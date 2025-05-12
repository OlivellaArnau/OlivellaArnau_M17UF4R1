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
    [SerializeField] private float _attackDamage = 50f;
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
        if (_isDead) return;

        // Prioridad de estados
        if (DetectPlayer() && _currentState != EnemyState.Attack)
        {
            if (_currentHealth >= _maxHealth / 2)
                ChangeState(EnemyState.Chase);
            else
                ChangeState(EnemyState.Flee);
        }
        else if (!DetectPlayer() && _currentState == EnemyState.Chase)
        {
            ChangeState(_patrolPoints.Count > 0 ? EnemyState.Patrol : EnemyState.Wander);
        }

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
                AttackBehavior();
                break;
        }

        HandleRotation();
        Debug.Log($"Current State: {_currentState}, Health: {_currentHealth}");
    }


    // --- Comportaments Principals ---
    private void PatrolBehavior()
    {
        if (_patrolPoints.Count == 0)
        {
            ChangeState(EnemyState.Wander);
            return;
        }

        if (Vector3.Distance(transform.position, _patrolPoints[_currentPatrolIndex].position) < 1f)
        {
            SetNextPatrolPoint();
        }
        _agent.SetDestination(_patrolPoints[_currentPatrolIndex].position);
    }

    private void ChaseBehavior()
    {
        if (Vector3.Distance(transform.position, _player.position) <= _attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        _agent.SetDestination(_player.position);
        _lastKnownPlayerPosition = _player.position;
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
        if (_agent.remainingDistance < 1f)
        {
            Vector3 randomDirection = Random.insideUnitSphere * _wanderRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            NavMesh.SamplePosition(randomDirection, out hit, _wanderRadius, 1);

            if (_patrolPoints.Count > 0)
            {
                // Si tiene puntos de patrulla, volver a patrullar después de un tiempo
                ChangeState(EnemyState.Patrol);
            }
            else
            {
                _agent.SetDestination(hit.position);
            }
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
    private void AttackBehavior()
    {
        if (Vector3.Distance(transform.position, _player.position) > _attackRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        if (!_isAttacking)
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

        _agent.isStopped = true;
        _animator.SetTrigger("Attack");

        // Rotación hacia el jugador durante el ataque
        float attackTimer = 0f;
        while (attackTimer < _attackActivationDelay)
        {
            RotateTowards(_player.position);
            attackTimer += Time.deltaTime;
            yield return null;
        }

        // Activar hitbox
        if (_attackHitbox != null)
            _attackHitbox.enabled = true;

        yield return new WaitForSeconds(_hitboxActiveDuration);

        // Desactivar hitbox y reanudar movimiento
        if (_attackHitbox != null)
            _attackHitbox.enabled = false;

        _agent.isStopped = false;
        _isAttacking = false;

        // Volver a Chase si el jugador sigue visible
        ChangeState(DetectPlayer() ? EnemyState.Chase :
                   _patrolPoints.Count > 0 ? EnemyState.Patrol : EnemyState.Wander);
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
    private void HandleRotation()
    {
        switch (_currentState)
        {
            case EnemyState.Chase:
            case EnemyState.Attack:
                RotateTowards(_player.position);
                break;

            case EnemyState.Patrol:
            case EnemyState.Wander:
                if (_agent.velocity.magnitude > 0.1f)
                    RotateTowardsMovementDirection();
                break;
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