using System;
using _Scripts.Counters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class ProgressBarUI : MonoBehaviour
    {
        [SerializeField] private Image barImage;
        [SerializeField] private GameObject hasProgressGameObject;
        
        private IHasProgress _hasProgress;

        private void Start()
        {
            _hasProgress = hasProgressGameObject.GetComponent<IHasProgress>();
            if (_hasProgress == null) return;
            _hasProgress.OnProgressChanged += HasProgress_OnProgressChanged;
            barImage.fillAmount = 0;
            SetActive(true);
        }

        private void OnDestroy()
        {
            if (_hasProgress == null) return;
            _hasProgress.OnProgressChanged -= HasProgress_OnProgressChanged;
        }

        private void HasProgress_OnProgressChanged(object sender, IHasProgress.OnProgressChangedEventArgs e)
        {
            barImage.fillAmount = e.ProgressNormalized;
            SetActive(e.ProgressNormalized is not (0 or 1f));
        }

        private void SetActive(bool value) => gameObject.SetActive(value);
    }
}