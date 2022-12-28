using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    public float focusRadius = 1f;
    public float focusCenterRatio = 0.1f;

    public float defaultDistance = 4f;
    public float minDistance = 1f;
    public float maxDistance = 6f;
    [Header("单次滚轮的最大距离")] public float scrollSpeed0 = 10f;
    [Header("视角手动拉近推远速度")] public float scrollSpeed1 = 10f;

    public float rotationXSpeed = 120f;
    public float rotationYSpeed = 90f;

    public float defaultVerticalAngles = 20f;
    public float maxVerticalAngles = 90f;

    public float gravityAlignSpeed = 180f;
    public float defaultAlignSpeed = 60f;
    public float alignHorizontalDelay = 0.3f;
    public float alignVerticalDelay = 1f;

    [Header("遮挡检测时的抬升角度")] public float maxOffsetAngle = 10f;

    [Range(30, 60)] public float minAlignAngles = 45f;
    [Range(150, 180)] public float maxAlignAngles = 150f;

    [Header("希望遮挡检测的层")] public LayerMask occlusionLayer;

    private Quaternion _lookRotation;
    private Quaternion _gravityRotation;
    private Quaternion _climbRotation;
    private Vector3 _upAxis;
    private Vector3 _focusPoint, _previousFocusPoint;
    private Vector3 _moveDir;
    private Vector3 _lookDirection;
    private Vector3 _lookPosition;
    private Vector3 _lastClimbNormal;
    private Vector2 _orbitAngles;
    private Transform _player;
    private Camera _camera;
    private float _lastManualRotateTime;
    private float _currentDistance;
    private float _offsetAngle;
    private bool _left;

    //TODO:摄像机攀爬自动化
    //TODO:优化楼梯时摄像机频繁移动
    //TODO:优化焦点跟随时，如果玩家停了下来，摄像机会沿直线插值过去，使用球面插值试试
    private void Start()
    {
        _orbitAngles = new Vector2(defaultVerticalAngles, 0f);
        _player = GameObject.Find("Player").transform;
        _camera = GetComponent<Camera>();
        _focusPoint = _player.position;
        _currentDistance = defaultDistance;
        _gravityRotation = Quaternion.identity;
    }

    private void LateUpdate()
    {
        _upAxis = CustomGravity.GetUpAxis(_player.position);
        UpdateFocusPoint();
        //这种写法在球形重力等等情况下有问题,会导致对齐不及时
        //_moveDir=new Vector3(_focusPoint.x - _previousFocusPoint.x, 0, _focusPoint.z - _previousFocusPoint.z);
        //我们在判断时投影到UpAxis平面即可,只用XZ平面做判断是为了防止跳跃时自动对齐
        _moveDir = _focusPoint - _previousFocusPoint;
        ManualRotate();
        ManualPullAndPush();
        //重力对齐的判定有两种方式
        //一个是和AutoAlign()一样，每帧都对齐
        //一个是检测范围，只有进入了重力某个范围才进行对齐
        //前者对齐在大重力范围时很有用，而后者会过于突兀，看情况选择了
        // if (CustomGravity.IsInGravityHeight1(_player.position))
        // {
        //     GravityAlign();
        // }
        GravityAlign();
        //玩家相对运动时时禁用移动对齐等
        if (CustomGravity.AllowCameraAlign)
        {
            MoveAlign();
        }
        _lookRotation = _gravityRotation * Quaternion.Euler(_orbitAngles)
                                         * Quaternion.Euler(_offsetAngle, 0, 0);
        _lookDirection = _lookRotation * Vector3.forward;
        _lookPosition = _focusPoint - _lookDirection * _currentDistance;
        //CameraOcclusion1();
        //CameraOcclusion2();
        CameraOcclusion();
        transform.SetPositionAndRotation(_lookPosition, _lookRotation);
    }

    private void UpdateFocusPoint()
    {
        Vector3 playerPoint = _player.position;
        float distance = Vector3.Distance(playerPoint, _focusPoint);
        float t = 1f;
        if (distance > 0.05f && focusRadius > 0)
        {
            t = Mathf.Pow(focusCenterRatio, Time.unscaledDeltaTime);
        }

        if (distance > focusRadius)
        {
            t = Mathf.Min(t, focusRadius / distance);
        }

        _previousFocusPoint = _focusPoint;
        _focusPoint = Vector3.Lerp(playerPoint, _focusPoint, t);
        CustomGravity.foucsPoint = _focusPoint;
    }

    private void ManualRotate()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        Vector2 input = new Vector2(-mouseY, mouseX);
        _lastManualRotateTime += Time.deltaTime;
        const float e = 0.01f;
        if (input.x < -e || input.x > e || input.y < -e || input.y > e)
        {
            _orbitAngles.x += rotationYSpeed * Time.deltaTime * input.x;
            _orbitAngles.y += rotationXSpeed * Time.deltaTime * input.y;
            _lastManualRotateTime = 0;
            _orbitAngles.x = Mathf.Clamp(_orbitAngles.x, -80f, maxVerticalAngles);
            _orbitAngles.y %= 360f;
        }
    }

    private void ManualPullAndPush()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        float desiredDistance = _currentDistance - scroll * scrollSpeed0;
        _currentDistance = Mathf.MoveTowards(_currentDistance, desiredDistance, scrollSpeed1 * Time.deltaTime);
        _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
    }

    /// <summary>
    /// 重力对齐,由于四元数旋转总是按固定方向旋转，在大角度时旋转会出现一些问题，教程中的写法应该是未优化
    /// 算是体会到四元数和欧拉角的优劣了，这种重力对齐应该是只能用四元数了，欧拉角表示起来太困难了
    /// 还有就是旋转时由于有旋转时间会导致在大角度旋转时会出现玩家弧线运动，在大角度对齐时禁用速度投影
    /// </summary>
    private void GravityAlign()
    {
        Vector3 start = _gravityRotation * Vector3.up;
        Vector3 up = _upAxis;
        float dot = Mathf.Clamp(Vector3.Dot(start, up), -1, 1);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        float maxAngle = gravityAlignSpeed * Time.deltaTime;
        //防止四元数不正常旋转，针对重力突然大角度旋转的情况，包括多球形重力切换等情况
        //主要是四元数总是进行最小旋转，有些旋转并不符合我们的直观感受，我们必须控制旋转方向
        //同时通过CustomGravity中间类禁用玩家的速度投影
        CustomGravity.AllowVelocityProjectMove = true;
        if (angle > 100f)
        {
            Vector3 forward = ProjectDirection(transform.forward, _upAxis);
            float signedAngle = Vector3.SignedAngle(start, up, -forward);
            up = signedAngle > 0 ? transform.right : -transform.right;
            CustomGravity.AllowVelocityProjectMove = false;
        }

        Quaternion newRotation = Quaternion.FromToRotation(start, up) * _gravityRotation;
        if (angle <= maxAngle)
        {
            _gravityRotation = newRotation;
        }
        else
        {
            _lastManualRotateTime = 0;
            //球形重力下，防止玩家低角度跳跃时脸擦地面，我们强制回正
            VerticalMoveAlign();
            _gravityRotation = Quaternion.SlerpUnclamped(_gravityRotation, newRotation, maxAngle / angle);
        }
    }

    #region MoveAlign

    private void MoveAlign()
    {
        //投影轴
        Vector3 moveDir = _moveDir - _upAxis * Vector3.Dot(_moveDir, _upAxis);
        //TODO：优化判定方式主要是自动适配不同速度
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            if (_lastManualRotateTime > alignHorizontalDelay)
            {
                //这种判定方法有问题，在球形重力下尤为突出
                //AutoAlignHorizontal();
                HorizontalMoveAlign1();
            }

            if (_lastManualRotateTime > alignVerticalDelay)
            {
                VerticalMoveAlign();
            }
        }
    }

    /// <summary>
    /// 仅针对平面移动，可摄像机输入空间，也可不摄像机输入空间
    /// 主要是不摄像机输入空间涉及到对齐时旋转方向的问题，不能向下面的一样一棍子打死
    /// </summary>
    private void HorizontalMoveAlign()
    {
        Vector3 moveDir = _moveDir.normalized;
        float angle = Vector3.SignedAngle(Vector3.forward, _moveDir, transform.up);
        if (angle < 0) angle += 360f;
        float absAngle = Mathf.Abs(Mathf.DeltaAngle(_orbitAngles.y, angle));
        float rotationChange = defaultAlignSpeed * Time.deltaTime;

        if (absAngle > maxAlignAngles)
        {
            angle = (angle + 180) % 360;
            moveDir = -moveDir;
            absAngle = Mathf.Abs(Mathf.DeltaAngle(_orbitAngles.y, angle));
        }

        if (absAngle < minAlignAngles) rotationChange *= absAngle / minAlignAngles;

        if (Mathf.Abs(_orbitAngles.y - angle) > 0.1f)
        {
            Vector3 cameraDir = Quaternion.Euler(0, _orbitAngles.y, 0) * Vector3.forward;
            Vector3 cross = Vector3.Cross(cameraDir, moveDir);
            _orbitAngles.y += cross.y < 0 ? -rotationChange : rotationChange;
            if (_orbitAngles.y < 0) _orbitAngles.y += 360f;
            if (_orbitAngles.y > 360) _orbitAngles.y -= 360f;
        }
        else
        {
            _orbitAngles.y = angle;
        }
    }

    /// <summary>
    /// 仅针对摄像机输入空间，各种重力源均可适用，主要是摄像机提供了方向，所以可以这样写
    /// 先前一版在球形重力下会出现赤道附近跟随慢，两极正常
    /// 主要是角度计算问题，需要用另外一种计算方式
    /// </summary>
    private void HorizontalMoveAlign1()
    {
        Vector3 moveDir = _moveDir.normalized;
        Vector3 forward = transform.forward;
        Vector3 up = CustomGravity.GetUpAxis(_player.position);
        forward = ProjectDirection(forward, up);
        moveDir = ProjectDirection(moveDir, up);
        float angle = Vector3.SignedAngle(forward, moveDir, up);
        float absAngle = Mathf.Abs(angle);
        absAngle = absAngle > 90 ? 180 - absAngle : absAngle;
        float angleChange = angle > 0 ? defaultAlignSpeed : -defaultAlignSpeed;
        if (CustomGravity.AllowVelocityProjectMove)
        {
            angleChange *= absAngle < minAlignAngles ? absAngle / minAlignAngles : 1;
        }

        _orbitAngles.y += angleChange * Time.deltaTime;
    }

    private void VerticalMoveAlign()
    {
        float rotationChange = defaultAlignSpeed * Time.deltaTime;
        _orbitAngles.x = Mathf.MoveTowards(
            _orbitAngles.x, defaultVerticalAngles, rotationChange);
    }

    #endregion
    
    private void CameraOcclusion()
    {
        Vector3 cameraHalfExtends;
        cameraHalfExtends.y = _camera.nearClipPlane * Mathf.Tan(0.5f * Mathf.Deg2Rad * _camera.fieldOfView);
        cameraHalfExtends.x = cameraHalfExtends.y * _camera.aspect;
        cameraHalfExtends.z = 0;
        Vector3 rectOffset = _lookDirection * _camera.nearClipPlane;
        Vector3 rectPosition = _lookPosition + rectOffset;
        Vector3 castPoint = _player.position;
        Vector3 castDir = rectPosition - castPoint;
        float castDistance = castDir.magnitude;
        castDir.Normalize();
        if (Physics.BoxCast(castPoint, cameraHalfExtends, castDir, out var hit
                , _lookRotation, castDistance, occlusionLayer,QueryTriggerInteraction.Ignore))
        {
            rectPosition = castPoint + castDir * hit.distance;
            _lookPosition = rectPosition - rectOffset;
        }
    }

    private void CameraOcclusion1()
    {
        Vector3 cameraHalfExtends;
        cameraHalfExtends.y = _camera.nearClipPlane * Mathf.Tan(0.5f * Mathf.Deg2Rad * _camera.fieldOfView);
        cameraHalfExtends.x = cameraHalfExtends.y * _camera.aspect;
        cameraHalfExtends.z = 0;
        Vector3 rectOffset = _lookDirection * _camera.nearClipPlane;
        Vector3 rectPosition = _lookPosition + rectOffset;
        Vector3 castPoint = _player.position;
        Vector3 castDir = rectPosition - castPoint;
        float castDistance = castDir.magnitude;
        castDir.Normalize();
        if (Physics.BoxCast(castPoint, cameraHalfExtends, castDir, out var hit
                , _lookRotation, castDistance, occlusionLayer))
        {
            rectPosition = castPoint + castDir * hit.distance;
            _lookPosition = rectPosition - rectOffset;
            //我们稍微抬升摄像机，针对摄像机低角度下楼梯时的频繁拉近，我们通过摄像机抬升避免过于频繁的拉近
            //主要是我这个楼梯碰撞网格比较大，可以使斜坡网格完全盖着楼梯网格就不会频繁拉近了
            if (_offsetAngle < maxOffsetAngle)
            {
                _offsetAngle += Time.deltaTime * 20;
            }
        }
        else
        {
            //如果碰不到东西，要先检测降低摄像机会不会引起碰撞
            if (_offsetAngle > 0f)
            {
                _offsetAngle -= Time.deltaTime * 10;
                if (CameraBoxCastByLift(cameraHalfExtends, _offsetAngle))
                {
                    _offsetAngle += Time.deltaTime * 10;
                }
            }
        }
    }

    /// <summary>
    /// 可以通过摄像机抬升的方式解决的话就不进行摄像机拉近了
    /// 针对摄像机低角度下楼梯时的频繁拉近，我们通过摄像机抬升避免过于频繁的拉近
    /// 一个小优化，起码不会频繁出现拉近了,不过这点主要是由于
    /// </summary>
    private void CameraOcclusion2()
    {
        Vector3 cameraHalfExtends;
        cameraHalfExtends.y = _camera.nearClipPlane * Mathf.Tan(0.5f * Mathf.Deg2Rad * _camera.fieldOfView);
        cameraHalfExtends.x = cameraHalfExtends.y * _camera.aspect;
        cameraHalfExtends.z = 0;
        Vector3 rectOffset = _lookDirection * _camera.nearClipPlane;
        Vector3 rectPosition = _lookPosition + rectOffset;
        Vector3 castPoint = _player.position;
        Vector3 castDir = rectPosition - castPoint;
        float castDistance = castDir.magnitude;
        castDir.Normalize();

        if (Physics.BoxCast(castPoint, cameraHalfExtends, castDir, out var hit
                , _lookRotation, castDistance, occlusionLayer))
        {
            if (!CameraBoxCastByLift(cameraHalfExtends, maxOffsetAngle))
            {
                if (_offsetAngle < maxOffsetAngle)
                {
                    _offsetAngle += Time.deltaTime * 20;
                }
            }
            //否则就去拉近
            else
            {
                rectPosition = castPoint + castDir * hit.distance;
                _lookPosition = rectPosition - rectOffset;
            }
        }
        else
        {
            //如果碰不到东西，要先检测降低摄像机会不会引起碰撞
            if (_offsetAngle > 0f)
            {
                _offsetAngle -= Time.deltaTime * 10;
                if (CameraBoxCastByLift(cameraHalfExtends, _offsetAngle))
                {
                    _offsetAngle += Time.deltaTime * 10;
                }
            }
        }
    }

    /// <summary>
    /// 抬升后的摄像机碰到东西就返回true
    /// </summary>
    private bool CameraBoxCastByLift(Vector3 cameraHalfExtends, float offsetAngle)
    {
        //为抬升摄像机的计算做准备
        Quaternion lookRotation1 = _gravityRotation * Quaternion.Euler(_orbitAngles)
                                                    * Quaternion.Euler(offsetAngle, 0, 0);
        Vector3 lookDirection1 = lookRotation1 * Vector3.forward;
        Vector3 lookPosition1 = _focusPoint - lookDirection1 * maxDistance;
        Vector3 rectOffset1 = lookDirection1 * _camera.nearClipPlane;
        Vector3 rectPosition1 = lookPosition1 + rectOffset1;
        Vector3 castPoint = _player.position;
        Vector3 castDir1 = rectPosition1 - castPoint;
        float castDistance1 = castDir1.magnitude;
        castDir1.Normalize();
        return Physics.BoxCast(castPoint, cameraHalfExtends, castDir1
            , lookRotation1, castDistance1, occlusionLayer);
    }

    private Vector3 ProjectDirection(Vector3 axis, Vector3 upAxis)
    {
        return (axis - upAxis * Vector3.Dot(axis, upAxis)).normalized;
    }
}