using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // Components
    private CharacterController _controller;
    private InputAction _moveAction, _lookAction, _jumpAction, _crouchAction, _sprintAction, _aimAction, _danceAction, _shootAction;
    private Animator _animator;
    private Transform _currentCameraHolder;
    
    private Vector3 _originalCameraPosition;
    private Quaternion _originalCameraRotation;


    // Movement
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _sprintSpeed = 8f;
    [SerializeField] private float _crouchSpeed = 2.5f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private float _lookSensitivity = 0.5f;

    // States
    private Vector3 _velocity;
    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isAiming;
    private bool _isCrouching;
    private bool _isDancing = false;


    // Camera
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private Transform _cameraPivot;
    [Header("Camera Holders")]
    [SerializeField] private Transform _firstPersonCameraHolder;
    [SerializeField] private Transform _thirdPersonCameraHolder;
    [SerializeField] private Transform _danceCameraHolder;
    private float _thirdPersonXRotation = 0f; // Rotación vertical en 3a persona
    private float _firstPersonXRotation = 0f; // Rotación vertical en 1a persona

    // Dance
    [Header("Dance Settings")]
    [SerializeField] private float _danceDuration = 2f;

    // Head
    [SerializeField] private GameObject _mageHead;

    [Header("Shooting")]
    [SerializeField] private Transform _muzzle; // Punt de sortida dels projectils (assigna un GameObject buit a la mà)
    [SerializeField] private GameObject _projectilePrefab; // Prefab del projectil (esfera, boles de foc, etc.)
    [SerializeField] private float _projectileSpeed = 20f;
    [SerializeField] private float _fireRate = 0.5f; // Temps entre trets
    private float _nextFireTime = 0f;
    
    [Header("Vida i Mort")]
    [SerializeField] private int _maxHealth = 100;
    private int _currentHealth;
    private bool _isDead = false;
    private void Start()
    {
        _currentHealth = _maxHealth;
    }
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        // Initialize Input Actions
        var playerInput = GetComponent<PlayerInput>();
        _moveAction = playerInput.actions["Move"];
        _crouchAction = playerInput.actions["Crouch"];
        _lookAction = playerInput.actions["Look"];
        _jumpAction = playerInput.actions["Jump"];
        _sprintAction = playerInput.actions["Sprint"];
        _aimAction = playerInput.actions["Aim"];
        _currentCameraHolder = _thirdPersonCameraHolder;
        _danceAction = playerInput.actions["Dance"];
        _shootAction = playerInput.actions["Shoot"];
    }

    private void Update()
    {
        if (!_isDancing) // Només processa inputs si no està ballant
        {
            HandleMovement();
            HandleLook();
            HandleJump();
            HandleSprint();
            HandleAim();
            HandleCrouch();
            HandleDance();
            HandleShooting();
        }
        UpdateAnimator();
    }

    private void HandleMovement()
    {
        _isGrounded = _controller.isGrounded;
        if (_isGrounded && _velocity.y < 0)
            _velocity.y = -2f; // Small force to stick to ground

        Vector2 moveInput = _moveAction.ReadValue<Vector2>();
        float currentSpeed = _isCrouching ? _crouchSpeed : (_isSprinting ? _sprintSpeed : _walkSpeed);

        Vector3 moveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        _controller.Move(moveDirection * currentSpeed * Time.deltaTime);

        // Apply gravity
        _velocity.y += _gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
    private void HandleLook()
    {
        Vector2 lookInput = _lookAction.ReadValue<Vector2>();
        float mouseX = lookInput.x * _lookSensitivity;
        float mouseY = lookInput.y * _lookSensitivity;

        // Rotación HORIZONTAL (compartida en ambos modos)
        transform.Rotate(Vector3.up * mouseX);

        // Rotación VERTICAL (dependiendo del modo)
        if (_isAiming)
        {
            // 1a PERSONA: Rota directamente la cámara
            _firstPersonXRotation -= mouseY;
            _firstPersonXRotation = Mathf.Clamp(_firstPersonXRotation, -90f, 90f);
            _cameraTransform.localRotation = Quaternion.Euler(_firstPersonXRotation, 0f, 0f);
        }
        else
        {
            // 3a PERSONA: Rota el pivot de la cámara
            _thirdPersonXRotation -= mouseY;
            _thirdPersonXRotation = Mathf.Clamp(_thirdPersonXRotation, 0f, 60f);
            _cameraPivot.localRotation = Quaternion.Euler(_thirdPersonXRotation, 0f, 0f);
        }
    }
    private void HandleJump()
    {
        if (_jumpAction.triggered && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            _animator.SetTrigger("Jump"); // Activa el trigger de salt
            _animator.SetBool("IsGrounded", false); // Desactiva l'estat de "a terra"
        }
    }

    private void HandleSprint()
    {
        _isSprinting = _sprintAction.IsPressed();
    }
    private void HandleCrouch()
    {
        _isCrouching = _crouchAction.IsPressed();
    }
    private void HandleAim()
    {
        bool wasAiming = _isAiming;
        _isAiming = _aimAction.IsPressed();

        if (_isAiming && !wasAiming)
        {
            // Guarda la rotación actual de 3a persona y reinicia la de 1a
            _thirdPersonXRotation = _cameraPivot.localEulerAngles.x;
            _firstPersonXRotation = 0f; // Reset para evitar acumulación
            _currentCameraHolder = _firstPersonCameraHolder;
            _mageHead.SetActive(false);
            StartCoroutine(SmoothCameraTransition(_firstPersonCameraHolder));
        }
        else if (!_isAiming && wasAiming)
        {
            // Restaura la rotación de 3a persona
            _cameraPivot.localRotation = Quaternion.Euler(_thirdPersonXRotation, 0f, 0f);
            _currentCameraHolder = _thirdPersonCameraHolder;
            _mageHead.SetActive(true);
            StartCoroutine(SmoothCameraTransition(_thirdPersonCameraHolder));
        }
    }

    private void HandleDance()
    {
        if (_danceAction.triggered && !_isDancing)
        {
            StartCoroutine(PerformDance());
        }
    }
    private IEnumerator PerformDance()
    {
        _isDancing = true;
        _originalCameraPosition = _cameraTransform.position;
        _originalCameraRotation = _cameraTransform.rotation;

        // Desactiva controls
        GetComponent<PlayerInput>().enabled = false;
        _animator.SetTrigger("Dance");

        // Mou càmera al Dance_Holder
        StartCoroutine(SmoothCameraTransition(_danceCameraHolder));

        // Espera la durada del ball
        yield return new WaitForSeconds(_danceDuration);

        // Restaura controls i càmera
        GetComponent<PlayerInput>().enabled = true;
        StartCoroutine(SmoothCameraTransition(_thirdPersonCameraHolder));
        _isDancing = false;
    }
    private void HandleShooting()
    {
        if (Time.time >= _nextFireTime && _isAiming) // Només es pot disparar apuntant
        {
            if (_shootAction.IsPressed())
            {
                Shoot();
                _animator.SetTrigger("Shoot"); // Activa el trigger de dispar   
                _nextFireTime = Time.time + _fireRate;
                
            }
        }
    }

    private void Shoot()
    {
        // Crea el projectil
        GameObject projectile = Instantiate(_projectilePrefab, _muzzle.position, _muzzle.rotation);
        Rigidbody rb = projectile.GetComponent<Rigidbody>();

        // Direcció del dispar (usant la rotació de la càmera)
        Vector3 shootDirection = _cameraTransform.forward; // En 1a persona
        if (!_isAiming) // En 3a persona, dispara cap endavant del personatge
            shootDirection = transform.forward;
            
        rb.AddForce(shootDirection * _projectileSpeed, ForceMode.VelocityChange);

        // Destrueix el projectil després de 3 segons (ajusta segons necessitis)
        Destroy(projectile, 3f);
    }
    // Afegir mètodes per Crouch, Dance, Shoot...
    private void UpdateAnimator()
    {
        // Actualitza l'estat de "a terra" (per al landing)
        _animator.SetBool("IsGrounded", _isGrounded);

        // Si volem controlar la velocitat per a transicions walk/run (opcional)
        Vector2 moveInput = _moveAction.ReadValue<Vector2>();
        float speed = moveInput.magnitude * (_isSprinting ? _sprintSpeed : _walkSpeed);
        _animator.SetFloat("Speed", speed);
        _animator.SetBool("IsCrouching", _isCrouching);
        _animator.SetBool("IsAiming", _isAiming);
        _animator.SetBool("IsDancing", _isDancing);
    }
    private IEnumerator SmoothCameraTransition(Transform targetHolder)
    {
        float duration = 0.3f;
        float elapsed = 0f;

        // Guarda la posición y rotación originales (relativas al padre actual)
        Vector3 originalLocalPosition = _cameraTransform.localPosition;
        Quaternion originalLocalRotation = _cameraTransform.localRotation;

        // Cambia el padre de la cámara al nuevo holder
        _cameraTransform.SetParent(targetHolder);

        // Posición y rotación DESEADAS en el nuevo padre (normalmente Vector3.zero y Quaternion.identity)
        Vector3 targetLocalPosition = Vector3.zero;
        Quaternion targetLocalRotation = Quaternion.identity;

        while (elapsed < duration)
        {
            // Interpola suavemente desde la posición/rotación original hasta la deseada EN EL NUEVO PADRE
            _cameraTransform.localPosition = Vector3.Lerp(originalLocalPosition, targetLocalPosition, elapsed / duration);
            _cameraTransform.localRotation = Quaternion.Slerp(originalLocalRotation, targetLocalRotation, elapsed / duration);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Asegura la posición y rotación final
        _cameraTransform.localPosition = targetLocalPosition;
        _cameraTransform.localRotation = targetLocalRotation;
    }
    public void TakeDamage(int damage)
    {
        if (_isDead) return;

        _currentHealth -= damage;

        if (_currentHealth <= 0)
        {
            Die();
        }
        else
        {
            _animator.SetTrigger("Hurt"); // Animació de dolor (opcional)
        }
    }
    private void Die()
    {
        _isDead = true;
        _animator.SetTrigger("Die");

        // Desactiva el control del jugador
        GetComponent<CharacterController>().enabled = false;
        GetComponent<PlayerInput>().enabled = false;
    }
}