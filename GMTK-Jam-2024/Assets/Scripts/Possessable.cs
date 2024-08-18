using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Possessable : MonoBehaviour
{
    [Header("Movement Variables")]
    [SerializeField] UnityEngine.InputSystem.InputActionReference moveActionRef;
    [SerializeField] float maxSpeed;
    [SerializeField] float accModifier;
    [Space(5)]
    [SerializeField] Vector3 directionLastFrame;
    [SerializeField] float dirChangeThreshold = 0.01f;
#if UNITY_EDITOR
    [Space(5)]
    [SerializeField] bool visualizeMoveInput;
#endif

    [Header("Jump Controls")]
    [SerializeField] float jumpForce;
    public bool jumpInProgress = false;
    public float maxIncline;
    public float fallGravMultiplier = 1;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Jump Leeway Options")]
    [SerializeField] [Min(0)] float jumpBufferDuration = 0.1f;
    [Tooltip("How much extra time the player has to jump when they walk off a ledge and thus stop being grounded.")]
    [SerializeField] [Min(0)] float coyoteTime = 0.1f;

    [Header("Self Component References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private CapsuleCollider primaryCol;
    [SerializeField] private CapsuleCollider secondaryCol;

    [Header("Child Object References")]
    [SerializeField] Transform pickUpPivot;
    [SerializeField] GameObject characterModel;
    [SerializeField] GameObject cameraPivot;
    [SerializeField] Transform shadow;
    [SerializeField] float shadowFloorOffset;
    [SerializeField] LayerMask shadowLayerMask = ~0;

    [Header("External References")]
    [SerializeField] Animator anim;

    [Header("Rotation Controls")]
    [SerializeField] float rotationSpeed;
    [SerializeField] float minRotationDistance;

    bool groundedLastFrame;
    bool jumpInputHeld;

    /// <summary>
    /// Nullifies itself after <see cref="jumpBufferDuration"/> seconds when jump is pressed.<br/>
    /// If this isn't null, that means there's a jump currently buffered.<br/><br/>
    /// See <see cref="OnJumpPerformed(UnityEngine.InputSystem.InputAction.CallbackContext)"/>.
    /// </summary>
    Coroutine jumpBufferedTimer;
    Coroutine coyoteTimeTimer;
    Coroutine landSFXCooldown;

    //Accessors
    public Transform PickUpPivot { get { return pickUpPivot; } }
    public GameObject CharacterModel { get { return characterModel; } }

    public Vector3 Velocity { get => rb.velocity; }
    public Collider PrimaryCollider { get => primaryCol; }
    public Collider SecondaryCollider { get => secondaryCol; }


    void Start()
    {
        //Get references to components on the GameObject
        anim = transform.GetComponentInChildren<Animator>();

        //InputHub.Inst.Gameplay.Jump.performed += OnJumpPerformed;
    }
    private void OnDestroy()
    {
        //InputHub.Inst.Gameplay.Jump.performed -= OnJumpPerformed;
    }

    //---Input Events---//

    /// <summary>
    /// This is called when Jump is performed, which should be set to happen on both press and release.
    /// </summary>
    private void OnJumpPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        //If we're pressing, input's held down. When we release, this'll be set to false.
        //jumpInputHeld = InputHub.Inst.Gameplay.Jump.WasPressedThisFrame();

        if (jumpBufferDuration > 0 && jumpInputHeld)
        {
            Coroutilities.TryStopCoroutine(this, ref jumpBufferedTimer);
            jumpBufferedTimer = Coroutilities.DoAfterDelay(this, () => jumpBufferedTimer = null, jumpBufferDuration);
        }
    }

    //---Core Methods---//

    private void Update()
    {
        //Update Drop Shadow 
        RaycastHit hit;
        if (shadow && Physics.Raycast(
            transform.position.Adjust(1, -0.5f, true),
            Vector3.down,
            out hit,
            Mathf.Infinity,
            shadowLayerMask,
            QueryTriggerInteraction.Ignore))
        {
            //float yPos = hit.collider.bounds.center.y + hit.collider.bounds.extents.y;
            shadow.position = new Vector3(hit.point.x, hit.point.y + shadowFloorOffset, hit.point.z);
        }
    }
    void FixedUpdate()
    {
        var grounded = IsGrounded();

        anim.SetBool("Jumped", jumpInProgress);

        //Debug.Log(anim.GetAnimatorTransitionInfo(0).IsUserName("Landing"));

        ApplyMoveForce(grounded);
        if (jumpBufferedTimer != null) TryDoJump();

        //If we're not grounded, we were grounded last frame, and we're not jumping,
        if (coyoteTime > 0 && !grounded && groundedLastFrame && !jumpInProgress)
        {
            //Give the player some time to jump anyway, even though they're starting to fall.
            Coroutilities.TryStopCoroutine(this, ref coyoteTimeTimer);
            coyoteTimeTimer = Coroutilities.DoAfterDelay(this, () => coyoteTimeTimer = null, coyoteTime);
        }

        //If NOT grounded and fall gravity should be modified,
        if (!grounded && !Mathf.Approximately(fallGravMultiplier, 1))
        {
            //then, if we're falling or rising without the jump button held,
            if (rb.velocity.y < 0 || (rb.velocity.y > 0 && !jumpInputHeld))
            {
                //Apply extra force based on the multiplier (There's no "gravity scale" for 3D Rigidbodies).
                //Gravity's already applied once by default; if 1.01, apply the extra 0.01
                rb.velocity += Physics.gravity * (fallGravMultiplier - 1) * Time.deltaTime;
                //rb.AddForce(Physics.gravity * (fallGravMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        groundedLastFrame = grounded;
    }

    private void ApplyMoveForce(bool grounded)
    {
        Vector3 direction = moveActionRef.action.ReadValue<Vector2>();

        direction = Quaternion.LookRotation(Vector3.Cross(cameraPivot.transform.right, Vector3.up)) * direction.SwapAxes(1, 2);

        anim.SetFloat("Walk Speed", direction.sqrMagnitude);

        if (direction.sqrMagnitude > minRotationDistance * minRotationDistance)
            RotateCharacterModel(direction.normalized);

        //If we are not grounded, inputting significantly, and in a significantly different direction (axis delta > deadzone),
        if (!grounded
            && direction.sqrMagnitude > minRotationDistance * minRotationDistance
            && (Mathf.Abs(directionLastFrame.x - direction.x) > dirChangeThreshold
            || Mathf.Abs(directionLastFrame.z - direction.z) > dirChangeThreshold))
        {
            //Make velocity point in the direction of input; input direction with the XZ magnitude of velocity, with the Y component of velocity
            rb.velocity = (direction * Vector3.ProjectOnPlane(rb.velocity, Vector3.up).magnitude).Adjust(1, rb.velocity.y);
        }

        //If both axes are under max speed, apply force in direction.
        float percentHeld = direction.magnitude;
        if (Mathf.Abs(rb.velocity.x) < maxSpeed * percentHeld && Mathf.Abs(rb.velocity.z) < maxSpeed * percentHeld)
        {
            rb.AddForce(direction * accModifier, ForceMode.Force);
        }

        directionLastFrame = direction;
        DrawDebugMovementRays(direction);
    }

    private void TryDoJump()
    {
        //No jumping if you've already jumped
        if (jumpInProgress)
            return;

        //If not grounded, and not in coyote time, no jumping
        if (!IsGrounded() && coyoteTimeTimer == null)
            return;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(new Vector3(0f, jumpForce, 0f));

        jumpInProgress = true;
        anim.SetBool("Jumped", true);
        Coroutilities.TryStopCoroutine(this, ref jumpBufferedTimer);
    }

    public bool IsGrounded()
    {
        GetCapsuleCastParams(out _, out float radius, out Vector3 point1, out Vector3 point2);

        radius -= 0.02f;
        bool groundedCheck = Physics.CapsuleCast(
            point1, point2,
            radius, Vector3.down,
            out RaycastHit groundHit,
            0.1f,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        if (jumpInProgress && groundedCheck)
        {
            Coroutine c = Coroutilities.DoNextFrame(this, () => jumpInProgress = rb.velocity.y > 0);
        }
        return groundedCheck && groundHit.normal.y >= maxIncline;
    }

    private void RotateCharacterModel(Vector3 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon) return;
        characterModel.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
    }

    //---Helper Methods---//

    public void GetCapsuleCastParams(out float height, out float radius, out Vector3 top, out Vector3 bottom)
    {
        height = primaryCol.height * transform.localScale.y;
        radius = primaryCol.radius * transform.localScale.y;

        top = transform.position + Vector3.up * height / 2;
        top += Vector3.down * radius;   //Go from tip to center of cap-sphere

        bottom = transform.position + Vector3.down * height / 2;
        bottom += Vector3.up * radius;
    }

    private void DrawDebugMovementRays(Vector3 direction)
    {
#if UNITY_EDITOR
        if (!visualizeMoveInput) return;

        var lightGrey = new Color(0.75f, 0.75f, 0.75f, 0.8f);
        Debug.DrawRay(transform.position, Vector3.Cross(cameraPivot.transform.right, Vector3.up) * 2.5f, lightGrey.Adjust(2, 1));
        Debug.DrawRay(transform.position, cameraPivot.transform.right * 2.5f, lightGrey.Adjust(0, 1));

        Debug.DrawRay(transform.position, rb.velocity, Color.yellow.Adjust(3, 0.6f));
        Debug.DrawRay(transform.position, rb.velocity - Vector3.up * rb.velocity.y, Color.yellow);

        Debug.DrawRay(transform.position, direction, Color.white);
        UtilFunctions.DrawSphere(transform.position + direction, 0.15f, 6, 6, Color.white);
#endif
    }
}