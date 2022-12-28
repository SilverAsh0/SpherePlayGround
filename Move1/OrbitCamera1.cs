using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera1 : MonoBehaviour
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

    public float gravityAlignSpeed = 360f;
    public float defaultAlignSpeed = 60f;
    public float alignHorizontalDelay = 0.3f;
    public float alignVerticalDelay = 1f;

    [Range(30, 60)] public float minAlignAngles = 30f;

    [Header("希望遮挡检测的层")] public LayerMask occlusionLayer;

    private Quaternion _lookRotation;
    private Quaternion _gravityRotation;
    private Vector3 _focusPoint, _previousFocusPoint;
    private Vector3 _moveDir;
    private Vector3 _lookDirection;
    private Vector3 _lookPosition;
    private Vector2 _orbitAngles;
    private Transform _player;
    private Camera _camera;
    private float _lastManualRotateTime;
    private float _currentDistance;
    private float _currentAlignSpeed;

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
        UpdateFocusPoint();
        _moveDir = _focusPoint - _previousFocusPoint;
        ManualRotate();
        ManualPullAndPush();
        GravityAlign();
        AutoAlign();
        _lookRotation = _gravityRotation * Quaternion.Euler(_orbitAngles);
        _lookDirection = _lookRotation * Vector3.forward;
        _lookPosition = _focusPoint - _lookDirection * _currentDistance;
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
    /// 重力对齐,由于四元数旋转总是按固定方向旋转，
    /// 在180度这种大角度时会出现一些问题，需要人为调整怎么转过去
    /// </summary>
    private void GravityAlign()
    {
        Vector3 start = _gravityRotation * Vector3.up;
        Vector3 end = CustomGravity.GetUpAxis(_player.position);
        float dot = Mathf.Clamp(Vector3.Dot(start, end), -1, 1);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        //防止四元数不正常旋转，主要针对重力突然180度旋转的情况。
        //不过某些情况下，摄像机如果角度过高会出现先拉近玩家然后才对齐重力的情况
        if (angle > 150f) end = transform.right;
        float maxAngle = gravityAlignSpeed * Time.deltaTime;
        Quaternion newRotation = Quaternion.FromToRotation(start, end) * _gravityRotation;
        if (angle <= maxAngle)
        {
            _gravityRotation = newRotation;
        }
        else
        {
            //在反转大角度重力时禁用对齐
            _lastManualRotateTime -= Time.deltaTime;
            _gravityRotation = Quaternion.SlerpUnclamped(_gravityRotation, newRotation, maxAngle / angle);
        }
    }

    private void AutoAlign()
    {
        if (_moveDir.magnitude > 0.005f)
        {
            if (_lastManualRotateTime > alignHorizontalDelay)
            {
                //这种判定方法有问题，在球形重力下尤为突出
                //AutoAlignHorizontal();
                AutoAlignHorizontal1();
            }

            if (_lastManualRotateTime > alignVerticalDelay)
            {
                AutoAlignVertical();
            }
        }
    }

    /// <summary>
    /// 仅针对摄像机输入空间
    /// </summary>
    private void AutoAlignHorizontal1()
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
        angleChange *= absAngle < minAlignAngles ? absAngle / minAlignAngles : 1;
        _orbitAngles.y += angleChange * Time.deltaTime;
    }

    private Vector3 ProjectDirection(Vector3 axis, Vector3 upAxis)
    {
        return (axis - upAxis * Vector3.Dot(axis, upAxis)).normalized;
    }

    private void AutoAlignVertical()
    {
        float rotationChange = defaultAlignSpeed * Time.deltaTime;
        _orbitAngles.x = Mathf.MoveTowards(
            _orbitAngles.x, defaultVerticalAngles, rotationChange);
    }

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
                , _lookRotation, castDistance, occlusionLayer))
        {
            rectPosition = castPoint + castDir * hit.distance;
            _lookPosition = rectPosition - rectOffset;
        }
        /*else
        {
            float currentDistance = (_lookPosition - _focusPoint).magnitude;
            currentDistance = Mathf.MoveTowards(currentDistance,
                defaultDistance, 2 * Time.deltaTime);
            _currentDistance = currentDistance;
        }*/
    }
}