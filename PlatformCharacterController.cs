using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Facing
{
    RIGHT = 1,
    LEFT = -1
}

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformCharacterController : MonoBehaviour
{
    //Components
    Rigidbody2D rb;
    Transform colliderTransform;
    PlatformCharacterJuicer juicer;


    [SerializeField] int playerInput;
    //Speed variables - they are public cause fuck you
    [Header("Movement Settings")]
    public float acceleration = 7;
    public float currentFriction;
    public float friction = 0.825f; //the higher the physics step the closer this number needs to be to 1
    public float frictionAir = 0.9f;
    public float frictionLedge = 0.75f;
    public float ledgeProtectionLenght = 3f;
    public bool debugLedgeRay = false;
    public float maxSpeed = 25;

    [Header("Jump Settings")]
    public float coyoteTimeMax = 0.1f;
    public bool canCoyoteJump = true;
    float coyoteTimeCurrent = 0f;
    public float targetGravity;
    public bool jumping = false;
    bool jumpReleased = false;
    public float gravityStrenght = 3f;
    public float gravityStrenghtJumping = 1.5f;
    public float gravityFalloffSpeed = 5f;
    public float jumpHoldTime = 0f;
    public float jumpHoldTimeMax = 0.5f;
    public float jumpStrenght = 5f;
    public int maxJumpCount = 2;
    public int currentJumpCount = 0;
    public float colliderTiltAmount = 15f;
    [Header("Ground Check Settings")]
    public float groundCheckPauseTime = 0.1f;
    public bool groundCheckTimeout = false;
    public LayerMask groundLayer = default;
    public bool grounded = false;
    [Range(-1f, 1f)] public float groundHorizontalOffset = 0.1f;
    public float groundVerticalOffset = 0f;
    public float groundCastLenght = 2f;
    public bool debugRays = false;
    public Color noHitColor = Color.green;
    public Color hitColor = Color.red;
    [Header("Movement Info")]
    public Vector2 direction;
    public Facing facing = Facing.RIGHT;

    private bool _isBeingPushed;

    private float _pushedTimeLeft;
    public float pushedTimer;

    public ParticleSystem walkParticle;
    public ParticleSystem jumpParticle;

    private PlayerStats _playerStats;
    void Start()
    {
        facing = Facing.RIGHT;
        rb = GetComponent<Rigidbody2D>();
        colliderTransform = GetComponentInChildren<Collider2D>().GetComponent<Transform>();
        juicer = GetComponentInChildren<PlatformCharacterJuicer>();
        _playerStats = this.gameObject.GetComponent<PlayerStats>();
    }

    // Update is called once per frame
    void Update()
    {
        rb.gravityScale = gravityStrenght;
        playerInput = (int)Input.GetAxisRaw("Horizontal");
        GroundCheck();
        CoyoteJumping();
        JumpCheck();
        ColliderTilting();
        if (_isBeingPushed)
        {
            _pushedTimeLeft -= Time.deltaTime;
            if (_pushedTimeLeft <= 0)
            {
                _isBeingPushed = false;
                direction.x = rb.velocity.x;
            }
        }

        if (grounded && rb.velocity.magnitude > 2f && !_playerStats.inWater)
        {
            var emission = walkParticle.emission;
            emission.enabled = true;
        }
        else
        {
            var emission = walkParticle.emission;
            emission.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        LRMovement();
        Jump();
        if (!_isBeingPushed)
            Friction();
    }

    void CoyoteJumping()
    {
        if (!grounded)
        {
            if (canCoyoteJump)
            {
                coyoteTimeCurrent += Time.deltaTime;
                if (coyoteTimeCurrent > coyoteTimeMax)
                    canCoyoteJump = false;
            }

        }
    }

    void LRMovement()
    {
        float _currentDirection = playerInput * acceleration;

        direction.x += _currentDirection;
        direction.x = Mathf.Clamp(direction.x, -maxSpeed, maxSpeed);
        Facing previousFacing = facing;
        if (_currentDirection > 0)
        {
            facing = Facing.RIGHT;
        }
        else if (_currentDirection < 0)
        {
            facing = Facing.LEFT;
        }

        if (facing != previousFacing)
        {
            transform.Rotate(new Vector3(0, 180, 0));
        }
    }
    void JumpCheck()
    {
        if (Input.GetButtonDown("Jump"))
        {
            jumping = true;
            jumpHoldTime = 0f;
            jumpReleased = false;
        }

    }

    void Jump()
    {
        bool _groundedOrCoyoting = grounded || canCoyoteJump;

        if (jumping)
        {
            jumping = false;
            if (_groundedOrCoyoting ||
                (!_groundedOrCoyoting && currentJumpCount != maxJumpCount && currentJumpCount > 0))
            {
                StartCoroutine(GroundCheckTimeout()); //pause ground checking for a bit

                if (currentJumpCount > 0 && currentJumpCount != maxJumpCount)
                {
                    //here you are doublejumping
                    _playerStats.TakeJumpDamage();   
                }
                currentJumpCount--;
                rb.velocity *= new Vector2(1, 0);
                rb.AddForce(new Vector2(0, jumpStrenght + Mathf.Abs(direction.x * 0.15f)), ForceMode2D.Impulse);
                juicer.JumpSquish();
                canCoyoteJump = false;
                if (!_playerStats.inWater) jumpParticle.Play();
            }
        }

        if (Input.GetButton("Jump") && !jumpReleased)
        {
            if (jumpHoldTime < jumpHoldTimeMax)
            {
                jumpHoldTime += Time.deltaTime;
                rb.gravityScale = gravityStrenghtJumping;
            }
            else
            {
                //targetGravity = Mathf.Lerp(rb.gravityScale, gravityStrenght, Time.deltaTime * gravityFalloffSpeed);
                rb.gravityScale = gravityStrenght;
            }
        }
        else
        {
            //targetGravity = Mathf.Lerp(rb.gravityScale, gravityStrenght, Time.deltaTime * gravityFalloffSpeed);
            rb.gravityScale = gravityStrenght;
        }

        if (Input.GetButtonUp("Jump"))
            jumpReleased = true;

        //gravity
        if (rb.velocity.y < 0)
            rb.gravityScale = gravityStrenght;
    }

    void Friction()
    {
        if (grounded)
        {
            if (!edgeProtection() && playerInput == 0)
            {
                direction.x *= frictionLedge;
                currentFriction = frictionLedge;
            }
            else
            {
                direction.x *= friction;
                currentFriction = friction;
            }

        }
        else
        {
            direction.x *= frictionAir;
            currentFriction = frictionAir;
        }

        rb.velocity = new Vector2(direction.x, rb.velocity.y);
    }

    void GroundCheck()
    {
        if (groundCheckTimeout)
            return;

        float _leftXPosition = -((transform.localScale.x / 2) - (transform.localScale.x * groundHorizontalOffset));

        Vector3 _leftCastPosition = new Vector3(transform.position.x + _leftXPosition, transform.position.y + groundVerticalOffset, transform.position.z);
        RaycastHit2D _leftCast = Physics2D.Raycast(_leftCastPosition, Vector2.down, groundCastLenght, groundLayer);
        if (debugRays)
        {
            Color _rayColor = noHitColor;

            if (_leftCast)
                _rayColor = hitColor;

            else
                _rayColor = noHitColor;

            Debug.DrawRay(_leftCastPosition, Vector2.down * groundCastLenght, hitColor);
        }
        //calculate right hit position
        float _rightXPositon = ((transform.localScale.x / 2) - (transform.localScale.x * groundHorizontalOffset));
        Vector3 _rightCastPosition = new Vector3(transform.position.x + _rightXPositon, transform.position.y + groundVerticalOffset, transform.position.z);
        RaycastHit2D _rightCast = Physics2D.Raycast(_rightCastPosition, Vector2.down, groundCastLenght, groundLayer);

        if (debugRays)
        {
            Color _rayColor;

            if (_rightCast)
                _rayColor = hitColor;
            else
                _rayColor = noHitColor;

            Debug.DrawRay(_rightCastPosition, Vector2.down * groundCastLenght, hitColor);
        }

        //center position raycast
        Vector3 _rayCenterPositon = new Vector3(transform.position.x, transform.position.y + groundVerticalOffset, transform.position.z);
        RaycastHit2D _centerCast = Physics2D.Raycast(_rayCenterPositon, Vector2.down, groundCastLenght, groundLayer);
        if (debugRays)
        {
            Color _rayColor = noHitColor;
            if (_centerCast)
                _rayColor = hitColor;
            else
                _rayColor = noHitColor;

            Debug.DrawRay(_rayCenterPositon, Vector2.down * groundCastLenght, hitColor);
        }

        grounded = _leftCast || _centerCast || _rightCast;

        if (grounded)
        {
            currentJumpCount = maxJumpCount;
            jumpHoldTime = 0f;
            jumpReleased = false;
            canCoyoteJump = true;
            coyoteTimeCurrent = 0f;
        }
    }

    IEnumerator GroundCheckTimeout()
    {
        groundCheckTimeout = true;
        yield return new WaitForSeconds(groundCheckPauseTime);
        groundCheckTimeout = false;
    }

    void ColliderTilting()
    {
        if (rb.velocity.y != 0)
        {
            //moving to the right
            if (rb.velocity.x < 0)
            {
                Vector3 _targetRotation = new Vector3(0, 0, colliderTiltAmount);
                colliderTransform.rotation = Quaternion.Euler(_targetRotation);
            }
            if (rb.velocity.x > 0)
            {
                Vector3 _targetRotation = new Vector3(0, 0, -colliderTiltAmount);
                colliderTransform.rotation = Quaternion.Euler(_targetRotation);
            }
        }
        else
            colliderTransform.rotation = Quaternion.Euler(Vector3.zero);
    }

    public void Push(Vector2 force)
    {
        _isBeingPushed = true;
        _pushedTimeLeft = pushedTimer;
        rb.AddForce(force, ForceMode2D.Impulse);
    }

    bool edgeProtection()
    {
        Vector2 _diagonalLine = new Vector2((int)facing, -1);
        RaycastHit2D _diagonalCheck = Physics2D.Raycast(transform.position, _diagonalLine, ledgeProtectionLenght, groundLayer);
        if (debugLedgeRay)
            Debug.DrawRay(transform.position, _diagonalLine * ledgeProtectionLenght, Color.green);
        return _diagonalCheck;
    }

}
