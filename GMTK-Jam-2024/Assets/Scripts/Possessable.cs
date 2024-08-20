using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Possessable : MonoBehaviour
{
    [SerializeField] InputActionReference moveActionRef;
    [SerializeField] InputActionReference abilityActionRef;
    [SerializeField] InputActionReference lookActionRef;
    [SerializeField] InputActionReference enableLookActionRef;
    [Space(5)]
    [SerializeField] GroundChecker groundChecker;
    [NaughtyAttributes.ValidateInput("ValidateAbilityScript", "Invalid entry; abilityScript does not implement IAbility!")]
    [SerializeField] MonoBehaviour _abilityScript;
    IAbility abilityScript;

    [Header("Movement Variables")]
    [SerializeField] float maxSpeed = 5;
    [SerializeField] float accModifier = 50;
    [Space(5)]
    [Tooltip("The point at which a difference in input is considered a change in direction. Used for aerial movement adjustments.")]
    [SerializeField] float dirChangeThreshold = 0.05f;
    [SerializeField] float minMoveBeforeRotate = 0.01f;

    [Header("Debug")]
    [SerializeField] [NaughtyAttributes.ReadOnly] Vector3 directionLastFrame;
#if UNITY_EDITOR
    [SerializeField] bool visualizeMoveInput;
#endif

    [Header("Camera/Visuals")]
    [Tooltip("The object to use when determining what \"forward\" is, like the pivot point of the gameplay camera.")]
    [SerializeField] GameObject orienter;
    [SerializeField] bool clickAndDrag;
    [SerializeField] bool invertX;
    [SerializeField] bool invertY;
    [SerializeField] float lookSpeed = 100;
    [SerializeField] [VectorLabels("Min", "Max")] Vector2 pitchRange = new(-75, 75);
    [Tooltip("All the objects that should rotate to face the direction of movement.")]
    [SerializeField] GameObject[] objectsToRotate;
    //[SerializeField] Animator anim;

    Rigidbody rb;
    bool initSuccess;

    static float yawCurrent;
    static float pitchCurrent;
    static Quaternion targetRotation;

    public InputActionReference AbilityActionRef => abilityActionRef;
    public Vector3 Velocity => rb.velocity;



    private bool ValidateAbilityScript() => !_abilityScript || _abilityScript is IAbility;
    void Start()
    {
        if (_abilityScript is IAbility script) abilityScript = script;

        moveActionRef.action.actionMap.Enable();

        rb = GetComponent<Rigidbody>();

        targetRotation = orienter.transform.localRotation;
        targetRotation.eulerAngles = targetRotation.eulerAngles.Adjust(2, 0);
        yawCurrent = targetRotation.eulerAngles.y;
        pitchCurrent = -targetRotation.eulerAngles.x;

        JustGotPossessed();

        Coroutilities.DoNextFrame(this, () => initSuccess = true);
    }

    private void OnEnable()
    {
        // This code is for becoming possessed/enabled after Start() is run once, so bail out if Start hasn't happened yet or happened this frame.
        if (!initSuccess) return;

        JustGotPossessed();
    }
    private void JustGotPossessed()
    {
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        abilityActionRef.action.performed += OnAbilityUsed;
    }
    private void OnDisable()
    {
        rb.interpolation = RigidbodyInterpolation.None;
        abilityActionRef.action.performed -= OnAbilityUsed;
    }

    private void OnAbilityUsed(InputAction.CallbackContext ctx)
    {
        if (abilityScript != null) abilityScript.DoAbility();
    }



    private void Update()
    {
        Vector2 lookAxis = lookActionRef.action.ReadValue<Vector2>();

        if (lookAxis != Vector2.zero
            && (!clickAndDrag || enableLookActionRef.action.IsPressed()))
        {
            yawCurrent += lookSpeed * lookAxis.x * Time.deltaTime;
            //Axis for yaw is multiplied by -1 if invertX is true
            targetRotation = Quaternion.AngleAxis(yawCurrent, Vector3.up * (invertX ? -1 : 1));

            pitchCurrent += lookSpeed * lookAxis.y * Time.deltaTime;
            pitchCurrent = Mathf.Clamp(pitchCurrent, pitchRange.x, pitchRange.y);
            //Axis for pitch is multiplied by -1 if invertY is true
            targetRotation *= Quaternion.AngleAxis(pitchCurrent, Vector3.left * (invertY ? -1 : 1));
        }

        orienter.transform.localRotation = targetRotation;
    }



    void FixedUpdate()
    {
        ApplyMoveForce();
    }

    private void ApplyMoveForce()
    {
        if (maxSpeed <= 0) return;

        Vector3 direction = moveActionRef.action.ReadValue<Vector2>();
        float percentHeld = direction.magnitude;

        // Multiplying direction by this quaternion makes it so a "forward" input is forward *according to the direction the camera pivot is facing*
        // We need to cross with the orienter's right vector because we assume it'll be pitching/yawing; the only stable axis is X, i.e. right
        direction = Quaternion.LookRotation(Vector3.Cross(orienter.transform.right, Vector3.up)) * direction.SwapAxes(1, 2);

        //anim.SetFloat("Walk Speed", direction.sqrMagnitude);

        bool movingEnoughToRotate = direction.sqrMagnitude > minMoveBeforeRotate * minMoveBeforeRotate;
        if (movingEnoughToRotate) RotateObjectsTowardMovement(direction.normalized);

        // If lateral velocity is under max speed, apply force in direction.
        if (rb.velocity.Adjust(1, 0).magnitude < maxSpeed * percentHeld)
        {
            rb.AddForce(direction * accModifier, ForceMode.Force);
        }

        // If we have no grounded checker, ignore this. If we do have one and are not grounded,
        if (groundChecker && !groundChecker.IsGrounded())
        {
            var velSansY = rb.velocity.Adjust(1, 0);
            var newVel = velSansY;

            // If input direction is low enough that we wouldn't wanna rotate (close to zero), quarter our lateral speed (it's a magic number but counterpoint: game jam)
            if (!movingEnoughToRotate)
            {
                newVel = velSansY * 0.75f;
            }
            // Otherwise, if we're inputting in a different direction than last frame, rotate our velocity toward that direction
            else if (Mathf.Abs(directionLastFrame.x - direction.x) > dirChangeThreshold
                || Mathf.Abs(directionLastFrame.z - direction.z) > dirChangeThreshold)
            {
                newVel = Vector3.RotateTowards(velSansY, direction * velSansY.magnitude, Mathf.PI * 2, 0);
            }

            newVel = newVel.Adjust(1, rb.velocity.y);
            rb.velocity = newVel;
        }

        directionLastFrame = direction;
        DrawDebugMovementRays(direction);
    }



    private void RotateObjectsTowardMovement(Vector3 direction)
    {
        if (objectsToRotate.Length < 1) return;

        //Make a rotation where direction is foward; ensure the y component of direction is 0.
        var desiredRotation = Quaternion.LookRotation(direction.Adjust(1, 0));
        foreach (var obj in objectsToRotate)
        {
            obj.transform.rotation = desiredRotation;
        }
    }

    private void DrawDebugMovementRays(Vector3 direction)
    {
#if UNITY_EDITOR
        if (!visualizeMoveInput) return;

        var lightGrey = new Color(0.75f, 0.75f, 0.75f, 0.8f);
        Debug.DrawRay(transform.position, Vector3.Cross(orienter.transform.right, Vector3.up) * 2.5f, lightGrey.Adjust(2, 1));
        Debug.DrawRay(transform.position, orienter.transform.right * 2.5f, lightGrey.Adjust(0, 1));

        Debug.DrawRay(transform.position, rb.velocity, Color.yellow.Adjust(3, 0.6f));
        Debug.DrawRay(transform.position, rb.velocity - Vector3.up * rb.velocity.y, Color.yellow);

        Debug.DrawRay(transform.position, direction, Color.white);
        UtilFunctions.DrawSphere(transform.position + direction, 0.15f, 6, 6, Color.white);
#endif
    }
}