using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundChecker : MonoBehaviour
{
    [NaughtyAttributes.InfoBox("If no rigidbody is provided here, it will use the one attached to this game object.")]
    [SerializeField] Rigidbody rbToCheckWith;
    [SerializeField] [Min(0)] float maxCastDistance = 0.1f;
    [Tooltip("The percentage incline allowed before slipping/not being grounded; 0 is an uninclined floor, 0.5 is a 45 degree slope, etc.")]
    [SerializeField] [Range(0, 1)] float maxIncline;
    // Currently unused, but could be hacked to work by using SweepTestAll and manually discarding results that are outside these layers
    //[SerializeField] private LayerMask groundLayers = ~0;

    float lastCheckTime;
    bool grounded;
    RaycastHit lastHitInfo;

    private void Start()
    {
        if (!rbToCheckWith) rbToCheckWith = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Checks if this rigidbody is grounded by performing a <see cref="Rigidbody.SweepTest(Vector3, out RaycastHit)"/>.<br/>
    /// If this method has already been called during this fixed update cycle (see <see cref="Time.fixedTime"/>), it will return a cached result instead of redoing the cast.
    /// </summary>
    /// <returns>Whether <see cref="rbToCheckWith"/>'s <see cref="Rigidbody.SweepTest(Vector3, out RaycastHit)"/> hit a surface that doesn't exceed <see cref="maxIncline"/>.</returns>
    public bool IsGrounded(out RaycastHit hitInfo)
    {
        // If we've already done a raycast during this fixed update cycle, return those results instead of recalculating.
        if (Mathf.Approximately(Time.fixedTime, lastCheckTime))
        {
            hitInfo = lastHitInfo;
            return grounded;
        }
        lastCheckTime = Time.fixedTime;

        grounded = rbToCheckWith.SweepTest(Physics.gravity, out hitInfo, maxCastDistance, QueryTriggerInteraction.Ignore);
        lastHitInfo = hitInfo;

        if (grounded)
        {
            // Since maxIncline is a percentage of how inclined we are,
            // and normal's y component is 1 when the surface is a floor (perfectly flat),
            // therefore we want 1 - the percentage incline. i.e., if no incline is allowed, we only accept normal vectors of 1, i.e. 1 - 0.
            grounded = hitInfo.normal.y >= 1 - maxIncline;
        }

        return grounded;
    }
    /// <inheritdoc cref="IsGrounded(out RaycastHit)"/>
    public bool IsGrounded() => IsGrounded(out _);
}
