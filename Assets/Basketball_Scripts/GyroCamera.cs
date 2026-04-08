using UnityEngine;
using UnityEngine.Events;

public class GyroCamera : MonoBehaviour {

	private Gyroscope gyro;
	private bool gyroSupported;
	private Quaternion rotFix;

	[SerializeField]
	private Transform gameWorld;
	private float startY;

	public UnityEvent OnGyroIsNotSupported;

	void Start () 
	{
		gyroSupported = SystemInfo.supportsGyroscope;
		//Debug.Log("gyroSupported: " + gyroSupported);

		if (gyroSupported) 
		{
			gyro = Input.gyro;
			gyro.enabled = true;

			transform.parent.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
			rotFix = new Quaternion(0f, 0f, 1f, 0f);
		}
		else
		{
			//Your Logic
			OnGyroIsNotSupported.Invoke();
		}
	}

	void Update () 
	{
		if (gyroSupported)
			transform.localRotation = gyro.attitude * rotFix;
	}

	void ResetGyroRotation()
	{
		startY = transform.eulerAngles.y;
		gameWorld.rotation = Quaternion.Euler(0f, startY, 0f);
	}
}
