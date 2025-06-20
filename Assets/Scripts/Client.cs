using TMPro;
using UnityEngine;

public class Client : MonoBehaviour
{
	public GameObject clientPrefab;
	public TMP_Text label;
	public Vector3 offset = new Vector3(0, 1, 0);
	public Quaternion fixedRotation = Quaternion.identity;

	private void LateUpdate()
	{
		if (clientPrefab != null && label != null)
		{
			label.transform.position = clientPrefab.transform.position + offset;
			label.transform.rotation = fixedRotation;
		}
	}
}