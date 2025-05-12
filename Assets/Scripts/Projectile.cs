using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private int _damage = 20;
    [SerializeField] private GameObject _impactEffect;

    private void OnCollisionEnter(Collision collision)
    {
        // Aplica mal a l'enemic
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        if (enemy != null)
            enemy.TakeDamage(_damage);
        Destroy(gameObject);
    }
}