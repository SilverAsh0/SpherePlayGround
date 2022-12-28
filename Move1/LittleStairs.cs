using UnityEngine;

public class LittleStairs : MonoBehaviour
{
    
    private void OnCollisionEnter(Collision collisionInfo)
    {
        if (collisionInfo.gameObject.CompareTag("Player"))
        {
            Rigidbody rigidbody = collisionInfo.gameObject.GetComponent<Rigidbody>();
            Vector3 vector3 = rigidbody.velocity;
            vector3.y = 0;
            rigidbody.velocity = vector3;
        }
    }
}
