using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    // The different GameObject Layers used by various scripts
    public const int solidLayer = 1 << 8;
    public const int waterLayer = 1 << 4;


    [Header("Input")]
    // The input class
    public InputCtrl inputCtrl;

    [Header("Physics")]
    // The Rigidbody handles physics
    public Rigidbody body;
    // Colliders
    public GameObject colNormal;
    public GameObject colDash;

    // The stats of the player
    public float height = 1f;
    public float radius = 0.5f;

    // Physics states
    protected bool wasGrounded = true;
    protected RaycastHit hitGround;

    // MegaMan-related states
    public bool isDashJump = false;

    [Header("Checks")]
    // Movement checks
    public bool canMove = true;
    public bool useGravity = true;

    [Header("World")]
    // Movement vectors
    public Vector3 rightVec = Vector3.right;
    public Vector3 gravityVec = Vector3.down;

    [Header("Player Stats")]
    // Speed n stuff
    public float moveSpeed = 10f;
    public float dashSpeed = 20f;
    public float jumpForce = 25f;


    // Enable
    protected virtual void OnEnable()
    {
        inputCtrl = new InputCtrl();
        inputCtrl.OnEnable();
    }
    // Disable
    protected virtual void OnDisable()
    {
        inputCtrl.OnDisable();
    }
    // Start method
    public virtual void Start()
    {
        // Rigidbody
        body = GetComponent<Rigidbody>();

        body.useGravity = false;
        body.drag = 0;
        
        // Collider
        colNormal.SetActive(true);
        colDash.SetActive(false);
    }
    public virtual void Update()
    {
        // Updates the input states 
        inputCtrl.UpdateInputStates();

        // Input-based actions
        HandleInput();
    }
    public virtual void FixedUpdate()
    {
        // Applies physics
        ApplyPhysics();

        // Regular movement
        Move_Regular();
    }
    public virtual void OnDrawGizmosSelected()
    {
        // Shows visuals in the Unity Scene 
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position - transform.up * height, radius);
    }


    public void ApplyPhysics()
    {
        // Keeps track if the player is currently on the ground
        bool onGround = isGrounded(0.2f);

        // Gravity
        if (!onGround && useGravity)
        {
            // If in water, less gravity is applied
            if (isInWater)
                body.velocity += gravityVec * -Physics.gravity.y * Time.fixedDeltaTime * 0.5f;
            // Regular gravity application
            else
                body.velocity += gravityVec * -Physics.gravity.y * Time.fixedDeltaTime;

            // After gravity is added, wallslides are calculated to limit the max velocity
            if (isWallSliding)
            {
                float wallSlideSpeed = 4f;

                // Grabs the downwards velocity and limits its magnitude
                Vector3 grav = Vector3.Project(body.velocity, gravityVec);
                if (Vector3.Dot(gravityVec, grav) > wallSlideSpeed)
                    grav = gravityVec * wallSlideSpeed;
                body.velocity = Vector3.ProjectOnPlane(body.velocity, gravityVec) + grav;

                // Ends the dashjump status during a wallslide
                if (canMove)
                    isDashJump = false;
            }    
        }

        // When you land
        if (!wasGrounded && onGround)
        {
            // Stops vertical velocity if moving down
            Vector3 velH = Vector3.ProjectOnPlane(body.velocity, gravityVec);
            if (Vector3.Dot(gravityVec, body.velocity) > 0)
            {
                body.velocity = velH;
                isDashJump = false;
            }
        }

        // Updates previous ground state
        wasGrounded = onGround;
    }
    public virtual void HandleInput()
    {
        // Exits if can't move
        if (!canMove)
            return;

        // Jump
        if (inputCtrl.keyJump == InputCtrl.KeyState.Press)
        {
            // If touching the ground, you can jump
            if (isGrounded(0.3f))
            {
                // Sets to dashjump state if the dash button is held down
                if (inputCtrl.keyDash == InputCtrl.KeyState.Hold || inputCtrl.keyDash == InputCtrl.KeyState.Press)
                    isDashJump = true;

                // Sets the vertical velocity
                body.velocity = Vector3.ProjectOnPlane(body.velocity, gravityVec) + gravityVec * -jumpForce;
            }
            // Wall Jumps if wall sliding
            else if (isWallSliding)
                StartCoroutine(Cor_WallJump());
        }
        else if (inputCtrl.keyJump == InputCtrl.KeyState.Release)
        {
            // Prevents the player from reaching the peak jump height if they release the jump button early
            if (Vector3.Dot(body.velocity, gravityVec) < 0)
                body.velocity = Vector3.ProjectOnPlane(body.velocity, gravityVec) + Vector3.Project(body.velocity, gravityVec) * 0.25f;
        }

        // Dash
        if (inputCtrl.keyDash == InputCtrl.KeyState.Press && !isWalled(0.1f, true))
        {
            StartCoroutine(Cor_Dash());
        }
    }
    public virtual void Move_Regular()
    {
        // Prevents movement if can't move
        if (!canMove)
            return;

        // Sets the player's rotation
        if (inputCtrl.input.x != 0)
            transform.rotation = Quaternion.LookRotation(rightVec * inputCtrl.input.x, -gravityVec);

        // Calculates the horizontal move vector, depends on the ground's normal if on ground
        Vector3 moveVec = rightVec;
        if (isGrounded(0.3f) && !isWalled(0.1f, colDash.activeSelf))
        {
            // Rotates the ground normal
            moveVec = Quaternion.AngleAxis(90f, transform.right) * hitGround.normal;

            // Fixes the overall moveVec magnitude to make the angled horizontal velocity match the flat one
            float dot = Vector3.Dot(rightVec, moveVec);
            if (dot != 0)
                moveVec = moveVec / dot;
        }

        // Splits the horizontal velocity
        Vector3 velH = Vector3.ProjectOnPlane(body.velocity, gravityVec);
        Vector3 velV = Vector3.Project(body.velocity, gravityVec);

        // Finds the movement speed
        float speed = moveSpeed;
        if (isDashJump)
            speed = dashSpeed;

        // Calculates the final horizontal movement vector
        velH = moveVec * inputCtrl.input.x * speed;

        // Displaces the player vertically based on slope
        Vector3 velHFrameDisp = Vector3.Project(velH, gravityVec);
        velH = Vector3.ProjectOnPlane(velH, gravityVec);

        // Normal velocity
        body.velocity = velH + velV;
        // Adds height based on slope, single frame
        body.position += velHFrameDisp * Time.fixedDeltaTime;
    }
    // Empty :(
    public virtual void HandleAnimations() { }


    // The walljump
    public IEnumerator Cor_WallJump()
    {
        // Prevents the player from moving briefly
        canMove = false;

        // If dashing button pressed, make dashing after jump
        if (inputCtrl.keyDash == InputCtrl.KeyState.Hold || inputCtrl.keyDash == InputCtrl.KeyState.Press)
            isDashJump = true;
        else
            isDashJump = false;

        // Sets the jump velocity
        body.velocity = -transform.forward * (isDashJump ? dashSpeed : moveSpeed) - gravityVec * jumpForce;
        yield return new WaitForSeconds(0.15f);

        // Gives the player control again
        canMove = true;
    }
    public IEnumerator Cor_Dash()
    {
        // Prevents the player from moving and dashing
        canMove = false;
        useGravity = false;

        // Sets the ducking collider
        colNormal.SetActive(false);
        colDash.SetActive(true);

        // Preps the dash variablestuff
        float time = 0.3f;
        bool isAirDash = !isGrounded(0.3f);

        // Dash loop
        while (time > 0f)
        {
            // Situations that can break the dash
            // Leaving the ledge while ground dashing
            if (!isAirDash && !isGrounded(0.3f))
                break;
            // Hitting a wall with no ceiling above
            if (isWalled(0.1f, colDash.activeSelf) && !hasCeiling)
                break;
            // Has ceiling on top and no way to get down
            if (isGrounded(0.3f) && !hasCeiling)
            {
                if (inputCtrl.keyDash == InputCtrl.KeyState.None || inputCtrl.keyDash == InputCtrl.KeyState.Release)
                    break;
            }

            // Finds the movement vector
            Vector3 moveVec = rightVec;
            if (isGrounded(0.3f) && !isWalled(0.1f, colDash.activeSelf))
            {
                moveVec = Quaternion.AngleAxis(90f, transform.right) * hitGround.normal;

                float dot = Vector3.Dot(rightVec, moveVec);
                if (dot != 0)
                    moveVec = moveVec / dot;
            }

            // Final Movement Vector
            if (Vector3.Dot(transform.forward, moveVec) < 0)
                moveVec *= -1;
            body.velocity = moveVec * dashSpeed;

            // Dash Jump
            if (inputCtrl.keyJump == InputCtrl.KeyState.Hold && !isAirDash && !hasCeiling)
            {
                canMove = true;
                useGravity = true;
                body.velocity = transform.up * jumpForce;
                isDashJump = true;
                break;
            }

            // Decreases dash time
            time -= Time.fixedDeltaTime;

            // If dash can't stop, keep going and give player control.
            if (time <= 0.1f && isGrounded(0.3f) && hasCeiling)
            {
                time = 0.1f;
                if (inputCtrl.input.x != 0)
                    transform.rotation = Quaternion.LookRotation(rightVec * inputCtrl.input.x, -gravityVec);
            }
            yield return new WaitForFixedUpdate();
        }

        // Sets normal colliders
        colNormal.SetActive(true);
        colDash.SetActive(false);

        // Small post-jump animation if on ground
        if (!isDashJump)
        {
            if (!isAirDash && isGrounded(0.3f))
            {
                time = 0.2f;
                while (time > 0.0f)
                {
                    body.velocity = Vector3.MoveTowards(body.velocity, Vector3.zero, Time.deltaTime * 150f);

                    // Breaks if jump
                    if (inputCtrl.keyJump == InputCtrl.KeyState.Press)
                    {
                        body.velocity = Vector3.ProjectOnPlane(body.velocity, gravityVec) + gravityVec * -jumpForce;
                        break;
                    }

                    // Decreases timer
                    time -= Time.deltaTime;
                    yield return null;
                }
            }

            // Stops velocity 
            if (Vector3.Dot(body.velocity, gravityVec) > 0)
                body.velocity = Vector3.zero;
        }

        // Resets normal movement status
        canMove = true;
        useGravity = true;
    }

    // Checks if the player is touching the ground
    public bool isGrounded(float addDistance = 0.1f)
    {
        return Physics.SphereCast(transform.position, radius * 0.9f, gravityVec, out hitGround, height + addDistance - radius, solidLayer);
    }
    // Checks if there's a wall right in front of the player
    public bool isWalled(float addDistance = 0.1f, bool isDucking = false)
    {
        if (isDucking)
            return Physics.CheckSphere(transform.position - transform.up * (height * 0.5f - radius * 0.5f) + transform.forward * addDistance,
                                       radius, solidLayer);
        else
            return Physics.CheckCapsule(transform.position + transform.up * (height * 0.5f - radius * 0.5f) + transform.forward * addDistance,
                                        transform.position - transform.up * (height * 0.5f - radius * 0.5f) + transform.forward * addDistance,
                                        radius, solidLayer);
    }

    // Checks if the player is wallsliding
    public bool isWallSliding
    {
        get
        {
            return isWalled(0.1f, colDash.activeSelf) && inputCtrl.input.x != 0;
        }
    }
    // Checks if the player can stand up
    public bool hasCeiling
    {
        get
        {
            return Physics.CheckSphere(transform.position + transform.up * (height * 0.5f - radius * 0.9f),
                                       radius, solidLayer);
        }
    }
    // Checks if the player is in water
    public bool isInWater
    {
        get
        {
            if (colDash.activeSelf)
                return Physics.CheckSphere(transform.position - transform.up * (height * 0.5f - radius * 0.5f),
                                           radius, waterLayer);
            else
                return Physics.CheckCapsule(transform.position + transform.up * (height * 0.5f - radius * 0.5f),
                                            transform.position - transform.up * (height * 0.5f - radius * 0.5f),
                                            radius, waterLayer);
        }
    }

}
