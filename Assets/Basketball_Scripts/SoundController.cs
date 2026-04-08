using UnityEngine;
using System.Collections;

public class SoundController : MonoBehaviour {
	
	public AudioClip[] ballImpactFloor;
	public AudioClip[] ballImpactNet;
	public AudioClip[] ballImpactRing;
	public AudioClip[] ballImpactSheet;
	public AudioClip[] ballImpactPole;
	public AudioClip[] ballWoofs;
	public AudioClip ballInWind;
	public AudioClip goal;
	public AudioClip goalClear;
	public AudioClip goalClearSpecial;
	public AudioClip bonusOpen;
	public AudioClip newRecord;
	public AudioClip gameOver;
	public static SoundController data;
	private AudioSource thisAudio;
	private bool playedNR;
	
	void Awake () {
		data = this;
		thisAudio = GetComponent<AudioSource>();
	}
	
	public void Stop(){
		if(thisAudio != null && thisAudio.isPlaying) {
			thisAudio.Stop();
			thisAudio.clip = null;
			thisAudio.loop = false;
		}
	}
	
	public void playBallInWind(){
		if(thisAudio != null && !thisAudio.isPlaying && ballInWind != null) {
			thisAudio.clip = ballInWind;
			thisAudio.loop = true;
			thisAudio.Play();
		}
	}
	
	public void playGoal(){
		if (thisAudio != null && goal != null) thisAudio.PlayOneShot(goal);
	}
	
	public void playClearGoal(){
		if (thisAudio != null && goalClear != null) thisAudio.PlayOneShot(goalClear);
	}
	
	public void playClearSpecialGoal(){
		if (thisAudio != null && goalClearSpecial != null) thisAudio.PlayOneShot(goalClearSpecial);
	}
	
	public void playNewRecord(){
		if (thisAudio != null && newRecord != null) thisAudio.PlayOneShot(newRecord);
	}
	
	public void playGameOver(){
		if (thisAudio != null && gameOver != null) thisAudio.PlayOneShot(gameOver);
	}
	
}
