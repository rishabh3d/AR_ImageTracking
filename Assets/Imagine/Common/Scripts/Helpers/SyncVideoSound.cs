using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Imagine.WebAR.Samples
{
    public class SyncVideoSound : MonoBehaviour
    {
        [SerializeField] VideoPlayer video;
        [SerializeField] AudioSource sound;

        public float lastPos = 0;
        
        // v1.8.0: Throttle sync corrections to prevent iOS audio stuttering
        private float lastSyncTime = 0;
        private const float SYNC_COOLDOWN = 0.5f; // Minimum time between sync corrections
        private const float SYNC_THRESHOLD = 0.15f; // Drift threshold before correcting

        void Awake(){
            
        }
        void OnEnable(){
            StartCoroutine("SyncRoutine");
        }

        void OnDisable(){
            StopCoroutine("SyncRoutine");
        }
        
        IEnumerator SyncRoutine()
        {
            var videoRenderer = video.GetComponent<Renderer>();
            videoRenderer.enabled = false;

            while(!video.isPrepared){
                Debug.Log("Waiting video preparation");
                yield return null;
            }

            video.Play();
            sound.Play();

            video.time = lastPos;
            //sound.time = lastPos;

            while(true){
                if(video.time > 0.01f)
                {
                    videoRenderer.enabled = true;
                }
                else if(!sound.isPlaying){
                    sound.time = (float)video.time;
                    sound.Play();
                }
                    

                // v1.8.0: Throttled sync with cooldown to prevent iOS audio stuttering
                if(Mathf.Abs(sound.time - (float)video.time) > SYNC_THRESHOLD){
                    if(Time.unscaledTime - lastSyncTime > SYNC_COOLDOWN){
                        Debug.Log(sound.time + ", " + sound.clip.length);
                        sound.time = (float)video.time;
                        Debug.Log(sound.time + "=>" + video.time);
                        lastSyncTime = Time.unscaledTime;
                    }
                }
               

                lastPos = (float)video.time;
                //yield return new WaitForSeconds(0.05f);
                yield return null;
            }
        }

        
    }
}
