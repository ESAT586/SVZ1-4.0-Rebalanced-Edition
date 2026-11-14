using UnityEngine;

public class Billboard : MonoBehaviour
{
	private Transform myTransform;

	private Transform cameraTransform;

	private void Start()
	{
		myTransform = base.transform;
		CacheCamera();
	}

	private void LateUpdate()
	{
		if (myTransform == null)
		{
			myTransform = base.transform;
		}
		if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
		{
			CacheCamera();
		}
		if (cameraTransform == null)
		{
			return;
		}
		myTransform.LookAt(myTransform.position + cameraTransform.rotation * new Vector3(0f, 0f, 1f));
	}

	private void CacheCamera()
	{
		Camera camera = Camera.main;
		if (camera == null)
		{
			camera = Camera.current;
		}
		cameraTransform = ((camera != null) ? camera.transform : null);
	}
}
