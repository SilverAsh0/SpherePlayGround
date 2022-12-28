using UnityEngine;

public class LittleCube : MonoBehaviour
{
    private float _strength;

    private float _sleepDelay;

    private MeshRenderer _meshRenderer;

    private static Rigidbody _player;

    private Rigidbody _rigidbody;

    private Vector3 _lastPosition;
    private Vector3 _gravity;


    // Start is called before the first frame update
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (!_rigidbody) _rigidbody = gameObject.AddComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.mass = 0.1f;
        _rigidbody.drag = 0.1f;
        _meshRenderer = GetComponent<MeshRenderer>();
        _player = GameObject.Find("Player").GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        //应用自定义重力会使刚体不再陷入休眠，我们人工判定，注意人工判定只是不对刚体施加力
        //刚体是否休眠要看物理引擎是否认为刚体可以休眠
        if (_rigidbody.IsSleeping())
        {
            _meshRenderer.material.color = Color.gray;
            return;
        }

        if (_rigidbody.velocity.sqrMagnitude < 0.0001f)
        {
            if (_sleepDelay > 2f) return;
            _meshRenderer.material.color = Color.yellow;
            _sleepDelay += Time.deltaTime;
        }
        else
        {
            _meshRenderer.material.color = Color.red;
            _sleepDelay = 0;
        }

        //优化判断
        if ((_lastPosition - _rigidbody.position).sqrMagnitude > 0.001f)
        {
            _gravity = CustomGravity.GetGravity(_rigidbody.position);
        }

        _rigidbody.AddForce(_gravity, ForceMode.Acceleration);
        _lastPosition = _rigidbody.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Lifting(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        Lifting(collision);
    }

    private void Lifting(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            //_strength = 0.1f;
            _strength = _player.velocity.magnitude * 0.1f;
            //在玩家碰到小立方体时，小立方体总是紧贴地面，我们让它飘起来更有感觉
            Vector3 velocity1 = _rigidbody.velocity;
            velocity1 += _strength * -_gravity.normalized;
            _rigidbody.velocity = velocity1;
        }
    }
}