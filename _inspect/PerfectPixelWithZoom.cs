using System;
using CrusaderDE;
using UnityEngine;

public class PerfectPixelWithZoom : MonoBehaviour
{
	public static PerfectPixelWithZoom instance;

	public float pixelsPerUnit = 64f;

	public float pixelsPerUnitScale = 1f;

	public float zoomScaleMax = 4f;

	public float zoomScaleStart = 1f;

	public bool smoovZoom = true;

	public float smoovZoomDuration = 0.2f;

	public int screenWidth;

	public int screenHeight;

	public float cameraSize;

	public float cameraStartSize;

	public float cameraCurrentSize;

	public Camera cameraComponent;

	public float zoomStartTime;

	public float zoomScaleMin = 0.5f;

	public float zoomCurrentValue = 1f;

	public float zoomNextValue = 1f;

	public float zoomInterpolation = 1f;

	public float zoomPos = 3f;

	public float currentZoomScale => pixelsPerUnitScale;

	public bool midZoom => zoomInterpolation < 1f;

	public void Awake()
	{
		instance = this;
		if ((Object)(object)instance == (Object)null)
		{
			instance = this;
		}
		else if ((Object)(object)instance != (Object)(object)this)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}

	public void Start()
	{
		screenHeight = Screen.height;
		screenWidth = Screen.width;
		cameraComponent = ((Component)this).gameObject.GetComponent<Camera>();
		cameraComponent.orthographic = true;
		cameraStartSize = cameraComponent.orthographicSize;
		SetZoomImmediate(zoomScaleStart);
	}

	public void Update()
	{
		if (screenHeight != Screen.height || screenWidth != Screen.width)
		{
			screenHeight = Screen.height;
			screenWidth = Screen.width;
			UpdateCameraScale();
		}
		if (midZoom)
		{
			if (smoovZoom)
			{
				zoomInterpolation = (Time.time - zoomStartTime) / smoovZoomDuration;
			}
			else
			{
				zoomInterpolation = 1f;
			}
			pixelsPerUnitScale = Mathf.Lerp(zoomCurrentValue, zoomNextValue, zoomInterpolation);
			UpdateCameraScale();
		}
	}

	public void setCamaraToStartSize()
	{
		cameraCurrentSize = cameraComponent.orthographicSize;
		cameraComponent.orthographicSize = 40f;
	}

	public void returnCamaraToCurrentSize()
	{
		cameraComponent.orthographicSize = cameraCurrentSize;
	}

	public void ResetZoom()
	{
		zoomPos = 3f;
		SetZoomImmediate(zoomScaleStart);
	}

	public void UpdateCameraScale()
	{
		capZoom();
		cameraSize = (float)screenHeight / (pixelsPerUnitScale * pixelsPerUnit) * 0.5f;
		cameraComponent.orthographicSize = cameraSize;
	}

	public void SetUpSmoovZoom()
	{
		zoomStartTime = Time.time;
		zoomCurrentValue = pixelsPerUnitScale;
		zoomInterpolation = 0f;
	}

	public void SetZoomImmediate(float scale)
	{
		pixelsPerUnitScale = Mathf.Max(Mathf.Min(scale, zoomScaleMax), zoomScaleMin);
		UpdateCameraScale();
	}

	public bool CanUserExtraZoom()
	{
		if (Screen.width > 2560 || Screen.height > 1440)
		{
			return false;
		}
		switch (GameMap.tilemapSize)
		{
		case 400:
		case 500:
		case 600:
		case 700:
		case 800:
			if (Screen.width > 2560 || Screen.height > 1440)
			{
				return false;
			}
			break;
		case 300:
			if (Screen.width > 2300 || Screen.height > 1440)
			{
				return false;
			}
			break;
		case 200:
			if (Screen.width > 1500 || Screen.height > 900)
			{
				return false;
			}
			break;
		case 160:
			if (Screen.width > 1300 || Screen.height > 800)
			{
				return false;
			}
			break;
		}
		return true;
	}

	public void adjustZoom(float adjustment, bool loop = false)
	{
		if (GameData.Instance.game_type == 4)
		{
			EngineInterface.TutorialAction(3);
		}
		zoomPos += adjustment;
		if (CameraControls2D.instance.isMapLocked())
		{
			float num = 3f;
			float num2 = 1f;
			if (!CanUserExtraZoom())
			{
				num2 = 2f;
			}
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				num2 -= 1f;
			}
			if (zoomPos > num)
			{
				if (!loop)
				{
					zoomPos = num;
				}
				else
				{
					zoomPos = num2;
				}
			}
			if (zoomPos < num2)
			{
				if (!loop)
				{
					zoomPos = num2;
				}
				else
				{
					zoomPos = num;
				}
			}
		}
		else
		{
			if (zoomPos > 5f)
			{
				zoomPos = 5f;
			}
			if (zoomPos < 0f)
			{
				zoomPos = 0f;
			}
		}
		Zoom(zoomPos);
	}

	public void Zoom(float _zoomTo)
	{
		SetUpSmoovZoom();
		switch ((int)Math.Round(_zoomTo * 2f))
		{
		case 0:
			zoomNextValue = 0.125f;
			break;
		case 1:
			zoomNextValue = 0.175f;
			break;
		case 2:
			zoomNextValue = 0.25f;
			break;
		case 3:
			zoomNextValue = 0.35f;
			break;
		case 4:
			zoomNextValue = 0.5f;
			break;
		case 5:
			zoomNextValue = 0.7f;
			break;
		case 6:
			zoomNextValue = 1f;
			break;
		case 7:
			zoomNextValue = 1.5f;
			break;
		case 8:
			zoomNextValue = 2f;
			break;
		case 9:
			zoomNextValue = 3f;
			break;
		case 10:
			zoomNextValue = 4f;
			break;
		}
	}

	public void limitZoomOnResChange()
	{
		if (zoomPos <= 1f)
		{
			Zoom(2f);
		}
	}

	public float getZoom()
	{
		return pixelsPerUnitScale;
	}

	public float getTargetZoom()
	{
		if (midZoom)
		{
			return Math.Min(pixelsPerUnitScale, zoomNextValue);
		}
		return pixelsPerUnitScale;
	}

	public float getZWidth()
	{
		return (float)screenWidth / (pixelsPerUnitScale * pixelsPerUnit) * 0.5f;
	}

	public void capZoom()
	{
		float maxWidthZoom = getMaxWidthZoom();
		float maxHeightZoom = getMaxHeightZoom();
		float num = Mathf.Max(maxWidthZoom, maxHeightZoom);
		if (num > 1f)
		{
			num = 1f;
		}
		if (pixelsPerUnitScale < num)
		{
			pixelsPerUnitScale = num;
		}
	}

	public float getMaxWidthZoom()
	{
		return (float)screenWidth / (((float)GameMap.tilemapSize - 4f) / 2f) / pixelsPerUnit;
	}

	public float getMaxHeightZoom()
	{
		float num = FatControler.instance.SHLowerUIPoint;
		if (!MainControls.instance.IsUIVisible)
		{
			num = 0f;
		}
		return ((float)screenHeight - num) / (((float)GameMap.tilemapSize - 6f) / 2f) / (pixelsPerUnit / 2f);
	}
}
