using UnityEngine;

public class HeroControls
{
	public delegate void OnPlayerControlCallback();

	private Rect kMoveLeftTouchArea = new Rect(0f, 0f, SUIScreen.width / 2f, SUIScreen.height);

	private Rect kMoveRightTouchArea = new Rect(SUIScreen.width / 2f, 0f, SUIScreen.width / 2f, SUIScreen.height);

	public OnPlayerControlCallback onMoveLeft;

	public OnPlayerControlCallback onMoveRight;

	public OnPlayerControlCallback onDontMove;

	private bool mWasMoving;

	private KeyCode mCurrentKey = KeyCode.A;

	private void UpdatePCControls()
	{
		bool leftPressed  = PCInputManager.GetKey(PCInputManager.Action.MoveLeft)  || Input.GetKey(KeyCode.LeftArrow);
		bool rightPressed = PCInputManager.GetKey(PCInputManager.Action.MoveRight) || Input.GetKey(KeyCode.RightArrow);

		if (mCurrentKey == KeyCode.A || mCurrentKey == KeyCode.LeftArrow)
		{
			if (rightPressed)
			{
				onMoveRight();
				if (!leftPressed)
				{
					// We'll just stick to a generic key state since remapping is dynamic
					mCurrentKey = KeyCode.D;
				}
			}
			else if (leftPressed)
			{
				onMoveLeft();
			}
			else
			{
				onDontMove();
			}
		}
		else
		{
			if (leftPressed)
			{
				onMoveLeft();
				if (!rightPressed)
				{
					mCurrentKey = KeyCode.A;
				}
			}
			else if (rightPressed)
			{
				onMoveRight();
			}
			else
			{
				onDontMove();
			}
		}
	}

	private void UpdateMobileControls()
	{
		int num = 0;
		if (WeakGlobalInstance<SUIScreen>.instance.inputs.justTouched)
		{
			mWasMoving = false;
		}
		if (WeakGlobalInstance<SUIScreen>.instance.inputs.isTouching)
		{
			Vector2 position = WeakGlobalInstance<SUIScreen>.instance.inputs.position;
			if (mWasMoving || !WeakGlobalSceneBehavior<InGameImpl>.instance.HUD.IsTouchingHUD(position))
			{
				if (kMoveLeftTouchArea.Contains(position))
				{
					num = -1;
					mWasMoving = true;
				}
				else if (kMoveRightTouchArea.Contains(position))
				{
					num = 1;
					mWasMoving = true;
				}
			}
			else
			{
				mWasMoving = false;
			}
		}
		else
		{
			mWasMoving = false;
		}
		if (num < 0)
		{
			if (onMoveLeft != null)
			{
				onMoveLeft();
			}
		}
		else if (num > 0)
		{
			if (onMoveRight != null)
			{
				onMoveRight();
			}
		}
		else if (onDontMove != null)
		{
			onDontMove();
		}
	}

	private bool IsPCInputActive()
	{
		bool leftPressed  = PCInputManager.GetKey(PCInputManager.Action.MoveLeft)  || Input.GetKey(KeyCode.LeftArrow);
		bool rightPressed = PCInputManager.GetKey(PCInputManager.Action.MoveRight) || Input.GetKey(KeyCode.RightArrow);
		return leftPressed || rightPressed;
	}

	public void Update()
	{
		if (SVZMultiplayerUI.IsLobbyOpen)
		{
			if (onDontMove != null) onDontMove();
			return;
		}
		// If playing online multiplayer as Client (Player 2), local PC keys must NOT move Host's hero (mHero)!
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && !SVZMultiplayerManager.Instance.IsSamePC && SVZNetworkManager.Instance != null && !SVZNetworkManager.Instance.IsHost)
		{
			if (onDontMove != null) onDontMove();
			return;
		}

		// If player is actively pressing movement keys or Gamepad stick/buttons, prioritize PC controls!
		// This enables simultaneous Keyboard (A/D) + USB Mouse clicking on HUD items on Android TV / Chromebook / PC!
		if (IsPCInputActive())
		{
			UpdatePCControls();
			return;
		}

		if (Application.isMobilePlatform)
		{
			if (WeakGlobalInstance<SUIScreen>.instance != null && WeakGlobalInstance<SUIScreen>.instance.inputs != null && WeakGlobalInstance<SUIScreen>.instance.inputs.isTouching)
			{
				UpdateMobileControls();
			}
			else
			{
				// Hybrid support: enables Gamepad, Android TV Remote, Chromebook keyboard, and Emulators!
				UpdatePCControls();
			}
		}
		else
		{
			UpdatePCControls();
		}
	}
}
