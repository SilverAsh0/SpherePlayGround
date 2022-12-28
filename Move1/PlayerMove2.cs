using System;
using UnityEngine;

public class PlayerMove2 : MonoBehaviour
{
    //前者为玩家刚体，后两个用于判断相对运动
    private Rigidbody _body, _currentBody, _previousBody;
    private Transform _inputSpace;

    public float maxSpeed = 7f;
    public float maxAirAcceleration = 4f;
    public float maxAcceleration = 10f;

    public float maxClimbSpeed = 5f;
    [Header("最大攀爬加速度")] public float maxClimbAcceleration = 30f;
    [Header("最大抓地加速度，越大越有可能粘在攀爬表面")] public float maxGripAcceleration = 30f;

    public float maxSwimSpeed = 6f;
    public float maxSwimAcceleration = 6f;
    [Header("最大水阻力，越大水中移动阻力越大")] public float maxWaterDrag = 2f;
    [Header("涉水阈值")] public float waterThreshold = 3f;
    [Header("淹没偏移，大小应为玩家中心点到玩家头顶的距离")] public float submergenceOffset = 0.5f;
    [Header("淹没范围，大小应为玩家身高")] public float submergenceRange = 1f;

    public float jumpHeight = 2f;
    public int maxJumpCount = 2;
    [Header("是否沿着法线跳跃")] public bool isNormalJump = true;

    [Header("最大地面判定角度,越小移动越自由")] [Range(0, 90)]
    public float maxGroundAngle = 45f;

    [Header("最大爬楼梯判定角度，越小爬楼越自由")] [Range(0, 90)]
    public float maxStairsAngle = 45f;

    [Header("最大攀爬角度,maxGroundAngle到该值为攀爬范围")]
    public float maxClimbAngle = 140f;

    [Header("XZ平面角度，越小攀爬时越有可能左右移动")] public float switchClimbAngle1 = 20f;
    [Header("XY平面角度，越大攀爬时越有可能仰视移动")] public float switchClimbAngle2 = 65f;
    [Header("ZY平面角度，越大攀爬时越有可能俯视移动")] public float switchClimbAngle3 = 15f;

    [Header("简化碰撞网格所在的碰撞层，针对楼梯等情形")] public LayerMask simpleMeshLayer;
    [Header("可攀爬网格所在的碰撞层")] public LayerMask climbLayer;
    [Header("水所在的碰撞层")] public LayerMask waterLayer;

    private Vector3 _playerInput, _velocity, _relativeVelocity;
    private Vector3 _upAxis, _rightAxis, _forwardAxis;
    private Vector3 _groundNormal, _steepNormal, _jumpNormal, _climbNormal;
    private Vector3 _previousPosition;
    private Vector3 _relativePosition;

    private float _maxGroundRad;
    private float _maxStairsRad;
    private float _maxClimbRad;

    private int _currentJumpCount;

    //淹没度，用于检测是否受到浮力以及浮力大小
    private float _submergence;

    //按空格键跳跃，按左Shift键攀爬
    private bool _desiredJump, _desiredClimb;
    private bool _onGround;
    private bool _onSteep;
    private bool _climbing;
    private bool _inWater;
    private bool _swimming;
    private bool _inFissure;

    //TODO:优化裂缝中按攀爬移动抖动的问题

    #region UnityCallBackEvent

    private void Start()
    {
        _body = GetComponent<Rigidbody>();
        if (!_body) _body = gameObject.AddComponent<Rigidbody>();
        _body.useGravity = false;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
        _body.drag = 0f;
        _body.angularDrag = 0;
        _body.constraints = RigidbodyConstraints.FreezeRotation;
        _maxGroundRad = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        _maxStairsRad = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
        _maxClimbRad = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
        _inputSpace = GameObject.Find("Camera").transform;
    }

    private void FixedUpdate()
    {
        InitState();
        RelativeMove1();
        VelocityProjectMove();
        JumpByHeight();
        ResetState();
    }

    private void Update()
    {
        _playerInput.x = Input.GetAxis("Horizontal");
        _playerInput.z = Input.GetAxis("Vertical");
        _playerInput.y = _inWater ? Input.GetAxis("Dive") : 0;
        _playerInput = Vector3.ClampMagnitude(_playerInput, 1f);
        InputTransform();
        _desiredJump |= Input.GetButtonDown("Jump");
        _desiredClimb = Input.GetButton("Climb");
        //Debug.DrawLine(transform.position, transform.position + _relativeVelocity, Color.red)
        //Debug.DrawLine(transform.position, transform.position + _forwardAxis, Color.blue);
        //Debug.DrawLine(transform.position, transform.position + _rightAxis, Color.red);
        //Debug.DrawLine(transform.position, transform.position + _upAxis, Color.green);
        //Debug.DrawLine(transform.position, transform.position + _climbNormal, Color.red);
    }

    #endregion

    #region StateChange

    void InitState()
    {
        _velocity = _body.velocity;
        Vector3 gravity = CustomGravity.GetGravity(_body.position);
        CustomGravity.AllowCameraAlign = true;

        //检查玩家是否在空中，此时禁用自动对齐，并给法线赋默认值
        if (!(_onGround || _inFissure))
        {
            _groundNormal = _upAxis;
            _jumpNormal = _upAxis;
            CustomGravity.AllowCameraAlign = false;
        }

        //先检测是否在水中，这意味着在水中的状态拥有最高优先权
        if (_inWater)
        {
            //在下潜的时候以及速度过大时我们停止施加浮力
            if (_playerInput.y == 0 && _velocity.magnitude < maxSwimSpeed)
            {
                _velocity += (1 - _submergence * waterThreshold) * Time.fixedDeltaTime * gravity;
            }
        }
        //如果玩家在地面上并且接触了可攀爬表面如何处理，算是一个小优化
        //同时防止低速移动时仍然会冲出墙壁上沿，施加重力和抓地力
        else if (_desiredClimb && _onGround)
        {
            _velocity += (gravity - _climbNormal * maxGripAcceleration) * Time.deltaTime;
        }
        //攀爬时施加抓地力并停止施加重力，并禁止自动对齐
        else if (_climbing)
        {
            _velocity -= _climbNormal * (maxGripAcceleration * Time.deltaTime);
            CustomGravity.AllowCameraAlign = false;
        }
        //防止站在斜坡上滑落
        else if (_onGround && _velocity.sqrMagnitude < 0.01f)
        {
            //这种写法有些问题，在斜坡上会抖动，对物理引擎理解还是不够深
            //我们要做的应该是消除斜坡平面分量的速度
            //Vector3 temp1 = _groundNormal * Vector3.Dot(_groundNormal, gravity);
            //_velocity += (temp1 - gravity) * Time.deltaTime;
            //或者说什么也不写，默认停止时不施加重力也可以做到
        }
        else
        {
            _velocity += gravity * Time.deltaTime;
        }

        //施加水中阻力
        if (_inWater)
        {
            _velocity *= 1f - maxWaterDrag * _submergence * Time.deltaTime;
        }

        //Debug.DrawLine(transform.position, transform.position + gravity*0.5f);
        //相对运动时禁用自动对齐
        if ((_currentBody && _playerInput.sqrMagnitude < 0.01f)) CustomGravity.AllowCameraAlign = false;
    }

    void ResetState()
    {
        _body.velocity = _velocity;
        _relativeVelocity = Vector3.zero;
        _groundNormal = Vector3.zero;
        _steepNormal = Vector3.zero;
        _climbNormal = Vector3.zero;
        _previousBody = _currentBody;
        _currentBody = null;
        _onGround = false;
        _onSteep = false;
        _climbing = false;
        _inWater = false;
        _submergence = 0;
    }

    #endregion

    #region 碰撞回调事件

    private void OnCollisionEnter()
    {
        _currentJumpCount = 0;
    }

    /// <summary>
    /// 只要玩家不在空中都会调用
    /// </summary>
    private void OnCollisionStay(Collision collision)
    {
        int collisionLayer = 1 << collision.gameObject.layer;
        bool canClimb = (collisionLayer & climbLayer) != 0;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float dot = Vector3.Dot(_upAxis, normal);

            //在地面上
            if (dot > _maxGroundRad)
            {
                _onGround = true;
                _groundNormal += normal;
                _currentBody = collision.rigidbody;
            }
            //不在地面时检测是不是在裂缝中或者墙壁上
            else
            {
                //在陡峭的地方,主要针对合地面法线是地面的情形
                if (dot > -0.01f)
                {
                    _onSteep = true;
                    _steepNormal += normal;
                    //检查玩家是否卡在裂缝中，在裂缝中我们也允许玩家移动
                    _inFissure = Vector3.Dot(_upAxis, _steepNormal) > _maxGroundRad;
                    if (!_onGround) _currentBody = collision.rigidbody;
                }

                //在可攀爬的地方
                if (dot > _maxClimbRad && _desiredClimb && canClimb)
                {
                    _climbing = true;
                    //_climbNormal += normal;
                    //不同于教程中的解决方法，我们这里在同一时间只允许一个攀爬法线起作用，防止卡内角
                    _climbNormal = normal;
                    _currentBody = collision.rigidbody;
                }
            }
        }

        _groundNormal.Normalize();
        _steepNormal.Normalize();
        _climbNormal.Normalize();
    }

    /// <summary>
    /// 只要玩家碰到了水就会调用
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        int colliderLayer = 1 << other.gameObject.layer;
        _inWater = colliderLayer == waterLayer;
        if (Physics.Raycast(_body.position + _upAxis * submergenceOffset, -_upAxis,
                out RaycastHit hit, submergenceRange + 1f, waterLayer, QueryTriggerInteraction.Collide))
        {
            _submergence = 1 - hit.distance / submergenceRange;
        }
        //没有击中水表面时就意味着已经完全淹没了
        else
        {
            _submergence = 1f;
        }
    }

    #endregion

    /// <summary>
    /// 相对运动，如果玩家处于移动物体上，我们让玩家可以相对移动
    /// </summary>
    private void RelativeMove()
    {
        if (!_currentBody) return;
        if (!_currentBody.isKinematic) return;
        if (_currentBody.mass < _body.mass) return;
        if (_previousBody == _currentBody)
        {
            //未考虑旋转带来的影响
            Vector3 relativeMovement = _currentBody.position - _previousPosition;
            //现在已经求出了相对运动的速度，我们需要将这个速度加到玩家刚体上
            _relativeVelocity = relativeMovement / Time.fixedDeltaTime;
        }

        _previousPosition = _currentBody.position;
    }

    /// <summary>
    /// 考虑了旋转对相对运动的影响,在上面的写法中的我们用的是移动物体的坐标
    /// 在这里使用玩家坐标去计算速度
    /// </summary>
    private void RelativeMove1()
    {
        if (!_currentBody) return;
        if (!_currentBody.isKinematic) return;
        if (_currentBody.mass < _body.mass) return;
        if (_previousBody == _currentBody)
        {
            //为当前帧玩家应该去的世界坐标位置，很巧妙的写法
            //_currentBody.transform.TransformPoint(_relativePosition)
            //为上一帧的玩家世界坐标，这样写属于是把坐标系那一套玩明白了
            //_previousPosition
            //TODO:看懂这里
            Vector3 relativeMovement = _currentBody.transform.TransformPoint(_relativePosition) - _previousPosition;
            _relativeVelocity = relativeMovement / Time.fixedDeltaTime;
        }

        //当前帧玩家的世界坐标，在下一帧中做位置参考
        _previousPosition = _body.position;
        //当前帧玩家的相对坐标，在下一帧中做位置参考
        _relativePosition = _currentBody.transform.InverseTransformPoint(_previousPosition);
    }

    private void InputTransform()
    {
        //零重力情况下的坐标系
        var position = transform.position;
        float gravityValue = CustomGravity.GetGravityValue(position);
        _upAxis = CustomGravity.GetUpAxis(position);
        //不在地面时，主要针对无重力区域，玩家可以勉强操控
        if (gravityValue == 0)
        {
            _upAxis = _inputSpace.up;
            _rightAxis = _inputSpace.right;
            _forwardAxis = _inputSpace.forward;
            return;
        }

        //普通情况下的坐标系
        if (_inputSpace)
        {
            _forwardAxis = ProjectDirection(_inputSpace.forward, _upAxis);
            _rightAxis = ProjectDirection(_inputSpace.right, _upAxis);
        }
        else
        {
            _forwardAxis = ProjectDirection(Vector3.forward, _upAxis);
            _rightAxis = ProjectDirection(Vector3.right, _upAxis);
        }
    }

    private void VelocityProjectMove()
    {
        //当摄像机在执行大角度旋转时禁用掉速度投影
        //防止因为摄像机旋转带来的球体弧线运动
        if (!CustomGravity.AllowVelocityProjectMove) return;
        //攀爬时我们要更改XZ轴的判定条件以及最大速度和最大加速度
        Vector3 xAxis;
        Vector3 zAxis;
        float speedChange = 0;
        float speed = 0;

        //水中
        if (_inWater)
        {
            xAxis = _rightAxis;
            zAxis = _forwardAxis;
            speedChange = maxSwimAcceleration;
            speed = maxSwimSpeed;
        }
        //攀爬中
        else if (_climbing && _desiredClimb)
        {
            //我们更改特定角度下的摄像机视角即X字型，四个地方的方向各不相同
            //借此我们可以实现在墙面上更加舒服的跑酷
            //X字型的下方
            xAxis = Vector3.Cross(_climbNormal, _upAxis);
            zAxis = Vector3.Cross(xAxis, _climbNormal);

            #region 攀爬时不同角度不同移动方向

            //针对X的左右两个方向，主要是找到摄像机方向在攀爬坐标系下各个平面的投影角度
            //根据角度判断当前方向
            Vector3 pos = _inputSpace.position - CustomGravity.foucsPoint;
            Vector3 project4 = Vector3.Dot(zAxis, pos) * zAxis;
            Vector3 project5 = Vector3.Dot(xAxis, pos) * xAxis;
            Vector3 project6 = Vector3.Dot(_climbNormal, pos) * _climbNormal;
            Vector3 project1 = project4 + project5;
            Vector3 project2 = project5 + project6;
            Vector3 project3 = project4 + project6;
            //计算角度
            float signedAngle1 = Vector3.SignedAngle(zAxis, project1, _climbNormal);
            float signedAngle2 = Vector3.SignedAngle(-_climbNormal, project2, zAxis);
            //XZ平面角度
            float absAngle1 = Mathf.Abs(signedAngle1);
            //XY平面角度
            float absAngle2 = Mathf.Abs(signedAngle2);
            //YZ平面角度
            float signedAngle3 = Vector3.SignedAngle(zAxis, project3, -xAxis);
            //我们需要综合考虑两个平面的角度然后做出判断
            if (absAngle1 > switchClimbAngle1 && absAngle1 < 180f - switchClimbAngle1 &&
                absAngle2 > switchClimbAngle2 && absAngle2 < 180f - switchClimbAngle2)
            {
                Vector3 temp1 = zAxis;
                //切换为左方向
                if (signedAngle1 >= 0f)
                {
                    zAxis = -xAxis;
                    xAxis = temp1;
                }
                ////切换为右方向
                else
                {
                    zAxis = xAxis;
                    xAxis = -temp1;
                }
            }
            //X字的上方
            else if (signedAngle3 < 0)
            {
                zAxis = -zAxis;
                xAxis = -xAxis;
            }
            else if (signedAngle3 < switchClimbAngle3)
            {
                zAxis = -zAxis;
            }

            #endregion

            speedChange = maxClimbAcceleration;
            speed = maxClimbSpeed;
        }
        //地面上
        else if (_onGround || _inFissure)
        {
            xAxis = ProjectDirection(_rightAxis, _groundNormal);
            zAxis = ProjectDirection(_forwardAxis, _groundNormal);
            speedChange = maxAcceleration;
            speed = maxSpeed;
        }
        //空中
        else
        {
            xAxis = _rightAxis;
            zAxis = _forwardAxis;
            //优化判定，主要针对空中跳跃存在的不正常减速行为
            if (_playerInput.sqrMagnitude > 0.01f)
                speedChange = maxAirAcceleration;
            speed = maxSpeed;
        }

        //TODO:理解此处
        //当有了连接体后，所有的速度都是相对于连接体，死去的高中物理突然攻击我
        Vector3 relativeVelocity = _velocity - _relativeVelocity;
        Vector3 adjustment = Vector3.zero;
        adjustment.x = _playerInput.x * speed - Vector3.Dot(relativeVelocity, xAxis);

        if (_playerInput.y != 0)
        {
            adjustment.y = _playerInput.y * speed - Vector3.Dot(relativeVelocity, _upAxis);
        }

        adjustment.z = _playerInput.z * speed - Vector3.Dot(relativeVelocity, zAxis);
        adjustment = Vector3.ClampMagnitude(adjustment, speedChange * Time.deltaTime);
        _velocity += adjustment.x * xAxis + adjustment.z * zAxis - adjustment.y * _upAxis;
    }

    private void JumpByHeight()
    {
        if (_desiredJump)
        {
            _desiredJump = false;
            if (_currentJumpCount >= maxJumpCount)
            {
                return;
            }

            if (_onGround)
            {
                _jumpNormal = _groundNormal;
            }
            else if (_climbing)
            {
                _jumpNormal = (_climbNormal + _upAxis).normalized;
            }
            else if (_onSteep)
            {
                _jumpNormal = (_steepNormal + _upAxis * 2).normalized;
            }
            else if (_currentJumpCount < maxJumpCount)
            {
                _jumpNormal = _upAxis;
            }

            _currentJumpCount += 1;
            Vector3 jumpNormal = isNormalJump ? _jumpNormal : _upAxis;
            float projectSpeed = Vector3.Dot(_velocity, _jumpNormal);
            float jumpSpeed = Mathf.Sqrt(2f * 9.81f * jumpHeight);
            projectSpeed = Mathf.Max(jumpSpeed - projectSpeed, 0);
            _velocity += jumpNormal * projectSpeed;
        }
    }

    private Vector3 ProjectDirection(Vector3 axis, Vector3 upAxis)
    {
        return (axis - upAxis * Vector3.Dot(axis, upAxis)).normalized;
    }
}