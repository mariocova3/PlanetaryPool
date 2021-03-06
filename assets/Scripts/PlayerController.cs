﻿using UnityEngine;
using System.Collections;

/// <summary>
/// Behaviour to control the player object.
/// </summary>
public class PlayerController : MonoBehaviour
{
	const float timeForMaxPower = 2.0f; // Maximum amount of time the player can accumulate power by holding down the mouse button
	const float maxSpeed = 50.0F; // Capping speed to ensure smooth gameplay.

	bool mouseDown;
	float downTime;
	bool launched;

	Rigidbody rigidBodyComp;
	GravityController gravityController;

	public AudioClip launchSound;
	AudioSource launchSource;

	// Use this for initialization
	void Start()
	{
		launchSource = AudioHelper.CreateAudioSource(gameObject, launchSound);

		rigidBodyComp = GetComponent<Rigidbody>();
		gravityController = GetComponent<GravityController>();

		mouseDown = false;
		launched = false;
		ClearExpectedPath();
	}

	/// <summary>
	/// FixedUpdate is called at fixed intervals to allow smooth physics simulation.
	/// See Unity docs for more information.
	/// </summary>
	void FixedUpdate()
	{
		// Clamp the speed to our maximum allowed
		rigidBodyComp.velocity = Vector3.ClampMagnitude(rigidBodyComp.velocity, maxSpeed);

		// If we're preparing to launch, calculate where the player object will go based on our current power
		if (mouseDown && !launched)
		{
			SetExpectedPath();
		}
	}

	/// <summary>
	/// Calculates the initial velocity if the mouse is released this frame.
	/// </summary>
	/// <returns>The initial velocity.</returns>
	Vector3 CalculateInitialVelocity()
	{
		Vector3 initialVelocity = Vector3.zero;

		//Get the mouse position in screen coordinates
		Vector3 mousePos = Input.mousePosition;

		//Set distance from camera, equal to the distance the camera is from the stage
		mousePos.z = Camera.main.transform.position.y;

		//Convert screen coordinate to world coordinate at the set camera distance
		mousePos = Camera.main.ScreenToWorldPoint(mousePos);

		initialVelocity = maxSpeed * (mousePos - rigidBodyComp.position).normalized;

		float launchTime = Mathf.Min (Time.time - downTime, timeForMaxPower);
		initialVelocity = initialVelocity * (launchTime / timeForMaxPower);

		return initialVelocity;
	}

	void SetExpectedPath()
	{
		Vector3[] positions = new Vector3[UIManager.maxPositionDraws];
		Vector3[] velocities = new Vector3[UIManager.maxPositionDraws];
		
		//Initial position is our starting position
		positions[0] = rigidBodyComp.position;
		
		//Calculate initial velocity
		velocities[0] = CalculateInitialVelocity();
		
		//Reproduce physics engine position updates
		for(int i = 1; i < UIManager.maxPositionDraws; ++i)
		{
			Vector3 appliedForce = GravityController.CalculateGravity(positions[i-1], rigidbBodyComp.mass);
			
			//F = m*a, so a = F/m
			Vector3 appliedAcceleration = appliedForce/rigidBodyComp.mass;
			
			//Unity physics engine applies the new velocity and calculates the new
			//position based on it
			
			//v(1) = v(0)+ a*t
			velocities[i] = velocities[i-1] + appliedAcceleration * Time.fixedDeltaTime;
			positions[i] = positions[i-1] + (velocities[i] * Time.fixedDeltaTime);
			
			//This is done in FixedUpdate(), we need to do it for every simulated frame
			velocities[i] = Vector3.ClampMagnitude(velocities[i], maxSpeed);
		}

		// Pass our calculated positions to the UIManager to draw a path
		UIManager.Instance.SetExpectedPath(positions);
	}

	/// <summary>
	/// Clears UI visuals of our expected path.
	/// </summary>
	void ClearExpectedPath()
	{
		UIManager.Instance.SetExpectedPath(new Vector3[0]);
	}

	// Update is called once per frame
	void Update()
	{
		// Update the UIManager with our current position
		UIManager.Instance.ShowPlayerIndicator(rigidBodyComp.position);

		// Process mouse inputs if the GameplayManager says we can
		if (GameplayManager.Instance.CanShoot())
		{
			if (Input.GetMouseButtonDown(0) && !GameplayManager.Instance.UIBlocked)
			{
				MouseDown();
			}

			if (Input.GetMouseButtonUp(0) && mouseDown)
			{
				MouseUp();
			}
		}
	}

	/// <summary>
	/// Process a left mouse button press.
	/// </summary>
	void MouseDown()
	{
		mouseDown = true;
		downTime = Time.time;
	}

	/// <summary>
	/// Process a release of the left mouse button.
	/// </summary>
	void MouseUp()
	{
		ClearExpectedPath();
		mouseDown = false;

		// If we haven't launched, we should launch now
		if (!launched)
		{
			Vector3 launchForce = CalculateInitialVelocity();

			launchSource.Play();

			// Apply the launch force as a velocity change for instant movement
			rigidBodyComp.AddForce(launchForce, ForceMode.VelocityChange);
			launched = true;

			// The level has been started, so we should enable gravity now
			gravityController.SetGravityEnabled(true);

			// Notify the level manager that we have started moving
			LevelManager.Instance.OnPlayerActivated();
		}
	}

	/// <summary>
	/// Called when this object collides with another rigidbody.
	/// See Unity docs for more information.
	/// </summary>
	/// <param name="collision">The object representing the collision.</param>
	void OnCollisionEnter(Collision collision)
	{
		// We use tags to check what we collided with.
		// We only care about planets (and the goal planet by extension).
		if (collision.gameObject.CompareTag("Planet") || collision.gameObject.CompareTag("Goal"))
		{
			// We hit something that should cause us to stop, so stop
			LevelManager.Instance.OnPlayerStopped();
			gravityController.SetGravityEnabled(false);

			if (collision.gameObject.CompareTag("Planet"))
			{
				GameObject.Destroy(gameObject);
			}
		}
	}
}
