using UnityEngine;
using TMPro;

public class MobileSensor : MonoBehaviour
{
    public TMP_Text debugMeesage;

    private Vector3 currentVelocity;
    private Vector3 currentAcceleration;
    private Vector3 currentGravity;
    private Vector3 currentRotation;

    // Start is called before the first frame update
    void Start()
    {
        Input.gyro.enabled = true;

        currentAcceleration = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        currentGravity = GetGravity();

        UpdateAcceleration();

        UpdateVelocity();

        currentRotation = Input.gyro.attitude.eulerAngles;

        debugMeesage.text = $"Acc: {currentAcceleration} " +
            $"\n\nAccMag: {currentAcceleration.magnitude}" +
            $"\n\nVelocity: {currentVelocity}" +
            $"\n\nGyrocopy: {currentRotation}" +
            $"\n\nGravity: {-currentGravity}";
    }

    private void UpdateAcceleration(bool withGravity = false)
    {
        Vector3 acc = Vector3.zero;
        float period = 0.0f;

        foreach (AccelerationEvent evnt in Input.accelerationEvents)
        {
            acc += evnt.acceleration * evnt.deltaTime;
            period += evnt.deltaTime;
        }
        if (period > 0)
        {
            acc *= 1.0f / period;
        }

        if (!withGravity)
            acc -= GetGravity();

        currentAcceleration = Vector3.Lerp(currentAcceleration, acc, 0.5f);
    }

    private void UpdateVelocity()
    {
        currentVelocity += currentAcceleration * Time.deltaTime;

        if (currentAcceleration.magnitude < 0.01f)
            currentVelocity = Vector3.zero;
    }

    private Vector3 GetGravity()
    {
        return Input.gyro.gravity;
    }

    public Vector3 GetEuglerAngle()
    {
        return Input.gyro.attitude.eulerAngles;
    }
}
