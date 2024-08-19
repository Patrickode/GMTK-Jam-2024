using UnityEngine;

public class Billboard2DObject : MonoBehaviour
{
    [NaughtyAttributes.InfoBox("If null, will default to facing toward Camera.main." +
        "\n\n(More accurately, it mimics the rotation, thus faces away. But assuming 2D, that should be equivalent)")]
    [SerializeField] private Camera refCam;
    [Space(10)]
    [SerializeField] private bool lockXRotation;
    [SerializeField] private bool lockYRotation;
    [SerializeField] private bool lockZRotation;

    private void Start()
    {
        if (!refCam) refCam = Camera.main;
    }

    private void Update()
    {
        if (lockXRotation || lockYRotation || lockZRotation)
        {
            transform.rotation = Quaternion.Euler(
                lockXRotation ? transform.rotation.eulerAngles.x : refCam.transform.rotation.eulerAngles.x,
                lockYRotation ? transform.rotation.eulerAngles.y : refCam.transform.rotation.eulerAngles.y,
                lockZRotation ? transform.rotation.eulerAngles.z : refCam.transform.rotation.eulerAngles.z);
        }
        else
        {
            transform.rotation = refCam.transform.rotation;
        }
    }
}