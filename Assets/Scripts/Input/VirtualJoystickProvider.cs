using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class VirtualJoystickProvider : MonoBehaviour, IInputProvider, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
	[Header("Joystick Components")]
	public GameObject joystickContainer;
	public Image joystickBackground;
	public Image joystickKnob;

	[Header("Settings")]
	public float joystickRange = 50f;
	public bool showOnlyWhenTouching = true;
	public bool hideOnDesktopByDefault = true;

	private Vector2 joystickCenter;
	private Vector2 currentInput = Vector2.zero;
	private bool isDragging = false;
	private bool isVisibleOnDesktop = false;
	private Canvas parentCanvas;
	private RectTransform backgroundRect;
	private RectTransform knobRect;

	public Vector2 GetMovementInput()
	{
		return currentInput;
	}

	public bool IsMoving()
	{
		return currentInput.magnitude > 0.1f;
	}

	public void Initialize()
	{
		parentCanvas = GetComponentInParent<Canvas>();
		backgroundRect = joystickBackground.rectTransform;
		knobRect = joystickKnob.rectTransform;

		PositionJoystickAtBottomCenter();

#if UNITY_ANDROID || UNITY_IOS
            // Mobile: Show joystick based on showOnlyWhenTouching setting
            if (showOnlyWhenTouching)
            {
                joystickContainer.SetActive(false);
            }
#elif UNITY_STANDALONE
		if (hideOnDesktopByDefault)
		{
			joystickContainer.SetActive(false);
			isVisibleOnDesktop = false;
		}
		else
		{
			isVisibleOnDesktop = true;
		}
#endif

		Debug.Log("Virtual joystick provider initialized");
	}

	public void Cleanup()
	{
		if (joystickContainer != null)
		{
			joystickContainer.SetActive(false);
		}
		Debug.Log("Virtual joystick provider cleaned up");
	}

	private void PositionJoystickAtBottomCenter()
	{
		RectTransform containerRect = joystickContainer.GetComponent<RectTransform>();

		containerRect.anchorMin = new Vector2(0.5f, 0f);
		containerRect.anchorMax = new Vector2(0.5f, 0f);

		containerRect.anchoredPosition = new Vector2(0f, 100f);
		containerRect.sizeDelta = new Vector2(120f, 120f);

		Debug.Log("Virtual joystick positioned at bottom center");
	}

	private void Update()
	{
#if UNITY_STANDALONE
		if (Input.GetKeyDown(KeyCode.Tab))
		{
			ToggleDesktopVisibility();
		}
#endif
	}

	private void ToggleDesktopVisibility()
	{
#if UNITY_STANDALONE
		isVisibleOnDesktop = !isVisibleOnDesktop;

		if (!isDragging)
		{
			joystickContainer.SetActive(isVisibleOnDesktop);
		}

		Debug.Log($"Virtual joystick desktop visibility: {isVisibleOnDesktop}");
#endif
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		isDragging = true;

		if (showOnlyWhenTouching)
		{
			Vector2 localPoint;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(
				parentCanvas.transform as RectTransform,
				eventData.position,
				eventData.pressEventCamera,
				out localPoint);

			backgroundRect.anchoredPosition = localPoint;
			joystickContainer.SetActive(true);
		}
#if UNITY_STANDALONE
		else if (!joystickContainer.activeInHierarchy && isVisibleOnDesktop)
		{
			joystickContainer.SetActive(true);
		}
#endif

		joystickCenter = backgroundRect.anchoredPosition;
		UpdateJoystick(eventData);
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isDragging = false;
		currentInput = Vector2.zero;

		knobRect.anchoredPosition = Vector2.zero;

		if (showOnlyWhenTouching)
		{
			joystickContainer.SetActive(false);
		}
#if UNITY_STANDALONE
		else if (hideOnDesktopByDefault && !isVisibleOnDesktop)
		{
			joystickContainer.SetActive(false);
		}
#endif
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (isDragging)
		{
			UpdateJoystick(eventData);
		}
	}

	private void UpdateJoystick(PointerEventData eventData)
	{
		Vector2 localPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			backgroundRect,
			eventData.position,
			eventData.pressEventCamera,
			out localPoint);

		Vector2 offset = localPoint;

		if (offset.magnitude > joystickRange)
		{
			offset = offset.normalized * joystickRange;
		}

		knobRect.anchoredPosition = offset;

		currentInput = offset / joystickRange;
	}
}