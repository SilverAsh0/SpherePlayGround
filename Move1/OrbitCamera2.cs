using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera2 : MonoBehaviour
{
    public float focusRadius = 0f;
    public float focusCenterRatio = 0.5f;

    public float defaultDistance = 4f;
    public float minDistance = 1f;
    public float maxDistance = 6f;
    [Header("单次滚轮的最大距离")] public float scrollSpeed0 = 10f;
    [Header("视角手动拉近推远速度")] public float scrollSpeed1 = 10f;

    public float rotationXSpeed = 120f;
    public float rotationYSpeed = 90f;

    public float defaultVerticalAngles = 20f;
    public float maxVerticalAngles = 90f;

    public float defaultAlignSpeed = 60f;
    public float alignHorizontalDelay = 0.3f;
    public float alignVerticalDelay = 1f;
    [Range(30, 60)] public float minAlignAngles = 45f;
    [Range(150, 180)] public float maxAlignAngles = 150f;
    private Vector3 _focusPoint, _previousFocusPoint;
    private Vector3 _moveDir;
    private Vector2 _orbitAngles;
    private Transform _player;
    private float _lastManualRotateTime;
    private float _currentDistance;

    void Start()
    {
        _orbitAngles = new Vector2(defaultVerticalAngles, 0f);
        _player = GameObject.Find("Player").transform;
        _focusPoint = _player.position;
        _currentDistance = defaultDistance;
    }

    private void LateUpdate()
    {
        UpdateFocusPoint();
        _moveDir = new Vector3(_focusPoint.x - _previousFocusPoint.x, 0
            , _focusPoint.z - _previousFocusPoint.z);
        ManualRotate();
        ManualPullAndPush();
        AutoAlign0();
        //AutoAlign1();
        Quaternion lookRotation = Quaternion.Euler(_orbitAngles);
        Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = _focusPoint - lookDirection * _currentDistance;
        transform.SetPositionAndRotation(lookPosition, lookRotation);
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
    /// 初版写法，也是教程中的写法，以我现在的水平来看，
    /// 虽然实现细节和教程有出入，但结果应该是一样的。
    /// </summary>
    private void AutoAlign0()
    {
        if (_lastManualRotateTime > alignHorizontalDelay)
        {
            if (_moveDir.magnitude < 0.01f)
            {
                return;
            }

            _moveDir.Normalize();
            //当前应该对齐的角度
            float angle = Vector3.SignedAngle(Vector3.forward, _moveDir, Vector3.up);
            if (angle < 0) angle += 180;
            //代表自动对齐所需的最小角度
            float absAngle = Mathf.Abs(Mathf.DeltaAngle(_orbitAngles.y, angle));

            float rotationChange = rotationXSpeed * Time.deltaTime;
            //减慢小角度对齐，先快后慢
            //rotationChange *= absAngle / minAlignAngles;
            //减慢小角度和大角度对齐，先慢后快在慢的旋转
            //就个人测试而言会选择后者多一点，玩家如果频繁变换方向
            //就会有点晕，对大角度和小角度的相同处理可以让旋转更加平滑

            //不过目前这个写法有一些隐患，就是集中在else if那里，物体在-z轴时，
            //有时是+180度，有时是-180度沿-z轴移动时，你视角在左右两侧得到的对齐结果不一致
            //其次如果再将玩家的输入转化到摄像机坐标系下去移动，就会出现摄相机沿x，z轴会抖动
            //摄像机跨不过去180度那道坎。目前猜测教程中的写法也未解决该问题。
            if (absAngle < minAlignAngles)
            {
                rotationChange *= absAngle / minAlignAngles;
            }
            else if (180f - absAngle < minAlignAngles)
            {
                rotationChange *= (180f - absAngle) / minAlignAngles;
            }

            _orbitAngles.y = Mathf.MoveTowards(_orbitAngles.y,
                angle, rotationChange);
        }
    }

    /// <summary>
    /// 修复上述问题,解决上述问题的关键就是分清摄像机应该从哪里转过来的,需要用到叉乘
    /// </summary>
    private void AutoAlign1()
    {
        if (_moveDir.magnitude > 0.01f)
        {
            if (_lastManualRotateTime > alignHorizontalDelay)
            {
                //AutoAlignHorizontal0();
                AutoAlignHorizontal1();
            }

            if (_lastManualRotateTime > alignVerticalDelay)
            {
                AutoAlignVertical();
            }
        }
    }

    /// <summary>
    /// 绝对对齐,永远朝移动方向对齐
    /// </summary>
    private void AutoAlignHorizontal0()
    {
        Vector3 moveDir = _moveDir.normalized;
        //当前应该对齐的角度
        float angle = Vector3.SignedAngle(Vector3.forward, moveDir, Vector3.up);
        //转化为统一的正值角度（0-360）
        if (angle < 0) angle += 360f;
        //代表自动对齐所需的最小角度
        float absAngle = Mathf.Abs(Mathf.DeltaAngle(_orbitAngles.y, angle));
        float rotationChange = defaultAlignSpeed * Time.deltaTime;

        if (absAngle < minAlignAngles) rotationChange *= absAngle / minAlignAngles;

        if (Mathf.Abs(_orbitAngles.y - angle) > 0.1f)
        {
            //先拿到摄像机Y轴旋转下的方向向量 
            Vector3 cameraDir = Quaternion.Euler(0, _orbitAngles.y, 0) * Vector3.forward;
            Vector3 cross = Vector3.Cross(cameraDir, moveDir);
            _orbitAngles.y += cross.y < 0 ? -rotationChange : rotationChange;
            if (_orbitAngles.y < 0) _orbitAngles.y += 360f;
            if (_orbitAngles.y > 360) _orbitAngles.y -= 360f;
        }
    }

    /// <summary>
    /// 就近对齐
    /// </summary>
    private void AutoAlignHorizontal1()
    {
        Vector3 moveDir = _moveDir.normalized;
        //当前应该对齐的角度
        float angle = Vector3.SignedAngle(Vector3.forward, _moveDir, Vector3.up);
        //转化为统一的正值角度（0-360）
        if (angle < 0) angle += 360f;
        //代表自动对齐所需的最小角度
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
            //先拿到摄像机Y轴旋转下的方向向量 
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
    /// 垂直角度对齐
    /// </summary>
    private void AutoAlignVertical()
    {
        float rotationChange = defaultAlignSpeed * Time.deltaTime;
        _orbitAngles.x = Mathf.MoveTowards(
            _orbitAngles.x, defaultVerticalAngles, rotationChange);
    }
}