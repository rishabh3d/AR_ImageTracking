using UnityEngine;
using System.Collections;

//This script works with its ball. Inter alia it sends an events to any listeners about its actions like thorowed, goaled, failed.
public class BallControl : MonoBehaviour 
{
	public Material standardMaterial, fadeMaterial;
	[HideInInspector]
	public bool thrown, floored, passed1, passed2, failed, goaled, special, clear;
	private Color col;
	private float distance;
	[HideInInspector]
	public float maxHeight;
	public GameObject ring;
	[HideInInspector]
	public AudioSource audioSource;
	private Rigidbody thisRigidbody;
	
	public delegate void ThrowAction();
	public delegate void GoalAction(float distance,float maxHeight, bool floored, bool clear, bool special);
	public delegate void FailAction();
    public static event ThrowAction OnThrow;
    public static event GoalAction OnGoal;
    public static event FailAction OnFail;
	
	void Awake()
	{
		col = GetComponent<Renderer>().material.color;
		audioSource = GetComponent<AudioSource>();
		thisRigidbody = GetComponent<Rigidbody>();
	}
	
	void Update()
	{
		if(failed || goaled) 
		{
			ResetBall();
		}
		
		if(thisRigidbody.IsSleeping() && thrown && !failed) 
		{
			print("failed, not touched basket");
			SetFailed();
		}

		if(transform.position.y/2 > maxHeight)
		{
			maxHeight = transform.position.y/2;
		}
		
		if(transform.position.y > 10 && thisRigidbody.linearVelocity.y > 10 && Mathf.Abs(thisRigidbody.linearVelocity.x) < 20 && !special && !floored)
		{
			special = true;
		}
	}
	
	void OnEnable()
	{
		distance = (transform.position - ring.transform.position).magnitude;
		maxHeight = transform.position.y;
	}
	
	public void ResetBall()
	{
		thrown = floored = passed1 = passed2 = failed = goaled = special = false;
		col.a = 1;
		clear = true;
		GetComponent<Renderer>().material = standardMaterial;
		GetComponent<Renderer>().material.color = col;
	}
	
	public void SetThrown()
	{
		thrown = true;
		if(OnThrow != null)
			OnThrow();

		if (audioSource != null && SoundController.data != null && SoundController.data.ballWoofs != null && SoundController.data.ballWoofs.Length > 0)
		{
			audioSource.PlayOneShot(SoundController.data.ballWoofs[Random.Range(0, SoundController.data.ballWoofs.Length)], 1);
		}
	}
	
	public void SetGoaled()
	{
		if(!goaled && !failed) 
		{
			goaled = true;
			if(OnGoal != null)
				OnGoal(distance, maxHeight, floored, clear, special);
		}
	}
	
	public void SetFailed()
	{
		if(!failed && !goaled) 
		{
			failed = true;
			if(OnFail != null)
				OnFail();
		}
	}

	void OnTriggerStay(Collider other) 
	{
		if (other.gameObject.name == "PlayZone")
		{
			float Yspeed = Mathf.Abs(thisRigidbody.linearVelocity.y);

			if(!thrown || goaled)
				return;
			
			if(transform.position.y < ring.transform.position.y - 2 && Yspeed < 3.0f && !passed2)
			{
				SetFailed();
			}
		}
	}
	
	void OnTriggerExit(Collider other)
	{
		if(other.gameObject.name == "trigger2") 
		{
			thisRigidbody.linearDamping = 0;
			if(passed1)
				SetGoaled();
		}
	}
	
	void OnTriggerEnter(Collider other){
		switch (other.gameObject.name) {
			case "trigger1":
				if(!passed2)
					passed1 = true;
			break;
			case "trigger2":
				if (SoundController.data != null) PlayRandomClip(SoundController.data.ballImpactNet);
				passed2 = true;
				if(passed1)
					thisRigidbody.linearDamping = thisRigidbody.linearVelocity.magnitude/2;
				else
				{
					SetFailed();
					print("failed, touched basket");
				}
			
			break;
		}
		
		if(other.gameObject.tag == "deadZone") 
		{
			SetFailed();
			print("failed, deadZone");
		}
	}
	
	void OnCollisionEnter(Collision other)
	{
		switch (other.gameObject.tag)
		{
			case "ring":
			
				clear = false; 
				if (SoundController.data != null) PlayRandomClip(SoundController.data.ballImpactRing);
			break;
			
			case "floor":
			
				if(!floored) 
				{
					floored = true; 
				} 
				else
				{
					SetFailed();
					print("failed, floor");
				}

				if (SoundController.data != null) PlayRandomClip(SoundController.data.ballImpactFloor);
				break;

			case "board":
				if (SoundController.data != null) PlayRandomClip(SoundController.data.ballImpactSheet);
			break;
			case "pole":
				if (SoundController.data != null) PlayRandomClip(SoundController.data.ballImpactPole);
			break;
			case "net":
				if (SoundController.data != null) PlayRandomClip(SoundController.data.ballImpactNet);
			break;
			
		}
	}
	
	void PlayRandomClip(AudioClip[] clips)
	{
		if (audioSource == null || clips == null || clips.Length == 0)
			return;

		float speed = Mathf.Clamp(thisRigidbody.linearVelocity.magnitude, 0, 15);
		
		audioSource.pitch = 1.15f - speed / 50;
		audioSource.PlayOneShot(clips[Random.Range(0,clips.Length)],speed/8);
	}
	
}