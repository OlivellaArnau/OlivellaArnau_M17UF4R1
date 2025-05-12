using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

public enum EnemyState { Patrol, Chase, Flee, Wander, Attack }

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _fleeDistance = 5f;
    [SerializeField] private float _wanderRadius = 7f;

    [Header("Components")]
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _player;
    [SerializeField] private List<Transform> _patrolPoints = new List<Transform>();

    [Header("Attack Settings")]
    [SerializeField] private float _attackDamage = 10f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private BoxCollider _attackHitbox;
    [SerializeField] private float _attackActivationDelay = 0.3f;
    [SerializeField] private float _hitboxActiveDuration = 0.5f;

    [Header("Rotation Settings")]
    [SerializeField] private float _rotationSpeed = 10f;

    private int _currentHealth;
    private bool _isDead = false;
    private EnemyState _currentState;
    private Vector3 _originalPosition;
    private Vector3 _lastKnownPlayerPosition;
    private int _currentPatrolIndex = 0;
    private bool _isAttacking = false;
    private bool _hasDealtDamage = false;
    private float _lastAttackTime = 0f;

    private void Start()
    {
        _currentHealth = _maxHealth;
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _player = GameObject.FindGameObjectWithTag("Player").transform;
        _originalPosition = transform.position;
        _currentState = EnemyState.Patrol;
        SetNextPatrolPoint();
    }

    private void Update()
    {
        Debug.Log($"Current State: {_currentState}");
        if (_isDead) return;

        switch (_currentState)
        {
            case EnemyState.Patrol:
                PatrolBehavior();
                break;
            case EnemyState.Chase:
                ChaseBehavior();
                break;
            case EnemyState.Flee:
                FleeBehavior();
                break;
            case EnemyState.Wander:
                WanderBehavior();
                break;
            case EnemyState.Attack:
                // Rotación especial durante el ataque
                RotateTowards(_player.position);
                break;
        }

        // Rotación general para todos los estados excepto Attack
        if (_currentState != EnemyState.Attack && _agent.velocity.magnitude > 0.1f)
        {
            RotateTowardsMovementDirection();
        }

        AvoidOtherEnemies();
    }

    // --- Comportaments Principals ---
    private void PatrolBehavior()
    {
        if (Vector3.Distance(transform.position, _patrolPoints[_currentPatrolIndex].position) < 1f)
        {
            SetNextPatrolPoint();
        }

        if (DetectPlayer())
        {
            if (_currentHealth >= _maxHealth / 2)
                ChangeState(EnemyState.Chase);
            else
                ChangeState(EnemyState.Flee);
        }
    }
    private void ChaseBehavior()
    {
        _agent.SetDestination(_player.position);
        _lastKnownPlayerPosition = _player.position;

        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        if (distanceToPlayer <= _attackRange)
        {
            AttackPlayer();
        }
        else if (!DetectPlayer())
        {
            if (_currentHealth >= _maxHealth)
                ChangeState(EnemyState.Patrol);
            else
                ChangeState(EnemyState.Wander);
        }
    }
    private void FleeBehavior()
    {
        Vector3 fleeDirection = (transform.position - _player.position).normalized;
        Vector3 fleeDestination = transform.position + fleeDirection * _fleeDistance;
        _agent.SetDestination(fleeDestination);

        if (Vector3.Distance(transform.position, _player.position) > _detectionRange)
        {
            ChangeState(EnemyState.Wander);
        }
    }

    private void WanderBehavior()
    {
        if (Vector3.Distance(transform.position, _agent.destination) < 1f)
        {
            Vector3 randomPoint = Random.insideUnitSphere * _wanderRadius + _lastKnownPlayerPosition;
            NavMeshHit hit;
            NavMesh.SamplePosition(randomPoint, out hit, _wanderRadius, 1);
            _agent.SetDestination(hit.position);
        }
    }

    // --- Funcions Auxiliars ---
    private bool DetectPlayer()
    {
        return Vector3.Distance(transform.position, _player.position) <= _detectionRange;
    }
    private void RotateTowardsMovementDirection()
    {
        if (_agent.velocity.magnitude > 0.1f)
        {
            Vector3 direction = _agent.velocity.normalized;
            direction.y = 0; // Mantenemos la rotación solo en eje Y
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }

    // Método para rotación durante el ataque
    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
    }
    private void SetNextPatrolPoint()
    {
        _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Count;
        _agent.SetDestination(_patrolPoints[_currentPatrolIndex].position);
    }

    private void AvoidOtherEnemies()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, 2f, LayerMask.GetMask("Enemy"));
        foreach (var enemy in nearbyEnemies)
        {
            if (enemy.gameObject != gameObject)
            {
                Vector3 avoidanceDirection = transform.position - enemy.transform.position;
                _agent.velocity += avoidanceDirection.normalized * 0.5f;
            }
        }
    }

    private void ChangeState(EnemyState newState)
    {
        _currentState = newState;
        _animator.SetInteger("State", (int)newState);
    }

    public void TakeDamage(int damage)
    {
        _currentHealth -= damage;
        Debug.Log($"Enemy took damage: {damage}, Current Health: {_currentHealth}");

        if (_currentHealth <= 0)
            Die();
        else if (_currentHealth < _maxHealth / 2 && _currentState != EnemyState.Flee)
            ChangeState(EnemyState.Flee);
    }
    private void AttackPlayer()
    {
        if (CanAttack() && !_isAttacking)
        {
            StartCoroutine(PerformAttack());
        }
    }
    private bool CanAttack()
    {
        return Time.time >= _lastAttackTime + _attackCooldown;
    }
    private IEnumerator PerformAttack()
    {
        _isAttacking = true;
        _hasDealtDamage = false;
        _lastAttackTime = Time.time;
        Debug.Log("Attack initiated");

        ChangeState(EnemyState.Attack);
        _agent.isStopped = true;

        // Mantener la rotación hacia el jugador durante el ataque
        while (_isAttacking)
        {
            RotateTowards(_player.position);
            yield return null;
        }
        // Espera antes de activar el hitbox
        yield return new WaitForSeconds(_attackActivationDelay);

        // Activa el hitbox
        if (_attackHitbox != null)
            _attackHitbox.enabled = true;

        // Mantén el hitbox activo por un tiempo
        yield return new WaitForSeconds(_hitboxActiveDuration);

        // Desactiva el hitbox
        if (_attackHitbox != null)
            _attackHitbox.enabled = false;

        _isAttacking = false;
        _agent.isStopped = false;

        // Vuelve al estado de persecución
        if (DetectPlayer())
            ChangeState(EnemyState.Chase);
        else
            ChangeState(EnemyState.Patrol);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (_isAttacking && !_hasDealtDamage && other.CompareTag("Player"))
        {
            PlayerController playerController = other.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // Usamos Mathf.RoundToInt para convertir el float a int si es necesario
                playerController.TakeDamage(Mathf.RoundToInt(_attackDamage));
                _hasDealtDamage = true;
            }
        }
    }
    private void Die()
    {
        _isDead = true;
        _animator.SetTrigger("Die");
        _agent.enabled = false;
        if (_attackHitbox != null)
            _attackHitbox.enabled = false;
        Destroy(gameObject, 3f);
    }
}