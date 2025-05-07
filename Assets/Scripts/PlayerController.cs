using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // Components
    private CharacterController _controller;
    private InputAction _moveAction, _lookAction, _jumpAction, _crouchAction, _sprintAction, _aimAction;
    private Animator _animator;
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

    // Camera
    [SerializeField] private Transform _cameraTransform;
    private float _xRotation = 0f;

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
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
        HandleJump();
        HandleSprint();
        HandleAim();
        HandleCrouch();
        UpdateAnimator(); //MetodePerActualitzarAnimator
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

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 80f);

        _cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
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

    private void HandleAim()
    {
        _isAiming = _aimAction.IsPressed();
        // (Aquí aniria la lògica de canvi de càmera 3a → 1a persona)
    }
    private void HandleCrouch()
    {
        _isCrouching = _crouchAction.IsPressed();
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
    }
}