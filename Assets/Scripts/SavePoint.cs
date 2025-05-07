using UnityEngine;

public class SavePoint : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            particles.Play(); // Efecte visual
            GameManager.Instance.SaveGame(other.transform.position, other.transform.rotation);
        }
    }
}
