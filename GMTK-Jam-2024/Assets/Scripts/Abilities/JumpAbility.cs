using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[RequireComponent(typeof(Rigidbody))]
public class JumpAbility : MonoBehaviour, IAbility
{
    [NaughtyAttributes.Required]
    [SerializeField] GroundChecker groundChecker;
    [SerializeField] UnityEngine.InputSystem.InputActionReference jumpActionRef;

    [Header("Jump Controls")]
    [SerializeField] float jumpForce = 350;
    [SerializeField] float fallGravMultiplier = 1.75f;
    [SerializeField] [NaughtyAttributes.ReadOnly] bool jumpInProgress = false;

    [Header("Jump Leeway Options")]
    [SerializeField] [Min(0)] float jumpBufferDuration = 0.1f;
    [Tooltip("How much extra time the player has to jump when they walk off a ledge (and in the process stop being grounded).")]
    [SerializeField] [Min(0)] float coyoteTime = 0.1f;

    bool groundedLastFrame;
    Rigidbody rb;

    Coroutilities.CorouTimer jumpBuffer;
    Coroutilities.CorouTimer coyoteTimeTimer;

    public void DoAbility()
    {
        if (TryDoJump()) return;

        // If we're here, we input jump while we can't jump. Buffer that input.
        jumpBuffer.StartTimer(this, jumpBufferDuration);
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        jumpBuffer = new Coroutilities.CorouTimer(jumpBufferDuration);
        coyoteTimeTimer = new Coroutilities.CorouTimer(coyoteTime);
    }

    private void FixedUpdate()
    {
        if (jumpBuffer.IsRunning) TryDoJump();

        bool groundedCache = groundChecker.IsGrounded();

        if (!groundedCache)
        {
            // If coyote time is enabled (>0), and we were grounded last frame, and we're not already jumping,
            if (coyoteTime > 0 && groundedLastFrame && !jumpInProgress)
            {
                // Give the player some time to jump anyway, even though they're starting to fall.
                coyoteTimeTimer.StartTimer(this, coyoteTime);
            }

            if (!Mathf.Approximately(fallGravMultiplier, 1))
            {
                // then, if we're falling or rising without the jump button held,
                if (rb.velocity.y < 0 || (rb.velocity.y > 0 && !jumpActionRef.action.IsPressed()))
                {
                    // Apply extra force based on the multiplier (There's no "gravity scale" for 3D Rigidbodies).
                    // Gravity's already applied once by default; if 1.01, apply the extra 0.01
                    rb.velocity += (fallGravMultiplier - 1) * Time.deltaTime * Physics.gravity;
                }
            }
        }

        // If we are grounded and there's a jump in progress, wait a frame.
        // If velocity is positive after that, we started a jump last frame and are going up, as opposed to landing from a jump.
        else if (jumpInProgress) Coroutilities.DoNextFrame(this, () => jumpInProgress = rb.velocity.y > 0);

        groundedLastFrame = groundedCache;
    }

    /// <summary>
    /// Attempts to make this object jump. Returns true if successful.
    /// </summary>
    private bool TryDoJump()
    {
        // No jumping if you've already jumped
        if (jumpInProgress) return false;

        // If not grounded, and not in coyote time, no jumping
        if (!groundChecker.IsGrounded() && coyoteTimeTimer.IsNotRunning) return false;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(new Vector3(0f, jumpForce, 0f));

        jumpInProgress = true;
        jumpBuffer.StopTimer(this);

        return true;
    }
}
