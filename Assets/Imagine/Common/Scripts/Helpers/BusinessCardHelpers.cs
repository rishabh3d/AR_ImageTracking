using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL
using System.Runtime.InteropServices;
#endif

namespace Imagine.WebAR
{
    /// <summary>
    /// v1.8.0: Business card helper functions for making phone calls (tel:) 
    /// and composing emails (mailto:) directly from AR experiences.
    /// </summary>
    public class BusinessCardHelpers : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void OpenUrl(string url);
#endif

        /// <summary>
        /// Opens the device's phone dialer with the given phone number.
        /// </summary>
        /// <param name="phoneNumber">Phone number to call (e.g., "+1234567890")</param>
        public void MakePhoneCall(string phoneNumber)
        {
            var url = "tel:" + phoneNumber.Trim();
            Debug.Log("BusinessCardHelpers: MakePhoneCall -> " + url);
#if UNITY_EDITOR || !UNITY_WEBGL
            Application.OpenURL(url);
#else
            OpenUrl(url);
#endif
        }

        /// <summary>
        /// Opens the device's email client to compose an email to the given address.
        /// </summary>
        /// <param name="emailAddress">Email address to send to</param>
        public void ComposeEmail(string emailAddress)
        {
            var url = "mailto:" + emailAddress.Trim();
            Debug.Log("BusinessCardHelpers: ComposeEmail -> " + url);
#if UNITY_EDITOR || !UNITY_WEBGL
            Application.OpenURL(url);
#else
            OpenUrl(url);
#endif
        }

        /// <summary>
        /// Opens the device's email client with subject and body pre-filled.
        /// </summary>
        /// <param name="emailAddress">Email address to send to</param>
        /// <param name="subject">Email subject</param>
        /// <param name="body">Email body</param>
        public void ComposeEmailWithDetails(string emailAddress, string subject = "", string body = "")
        {
            var url = "mailto:" + emailAddress.Trim();
            var hasParams = false;
            
            if (!string.IsNullOrEmpty(subject))
            {
                url += "?subject=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(subject);
                hasParams = true;
            }
            if (!string.IsNullOrEmpty(body))
            {
                url += (hasParams ? "&" : "?") + "body=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(body);
            }
            
            Debug.Log("BusinessCardHelpers: ComposeEmailWithDetails -> " + url);
#if UNITY_EDITOR || !UNITY_WEBGL
            Application.OpenURL(url);
#else
            OpenUrl(url);
#endif
        }

        /// <summary>
        /// Opens a URL in a new browser tab.
        /// </summary>
        /// <param name="url">URL to open</param>
        public void OpenWebsite(string url)
        {
            Debug.Log("BusinessCardHelpers: OpenWebsite -> " + url);
#if UNITY_EDITOR || !UNITY_WEBGL
            Application.OpenURL(url);
#else
            OpenUrl(url);
#endif
        }
    }
}
