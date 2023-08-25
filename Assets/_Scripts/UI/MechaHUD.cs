using System;
using _Scripts.Controller;
using _Scripts.MechaParts.SO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class MechaHUD : MonoBehaviour
    {
        [SerializeField] private Image healthBar;
        [SerializeField] private Image boostBar;

        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI boostText;

        [SerializeField] private TextMeshProUGUI weightText;
        [SerializeField] private TextMeshProUGUI speedText;

        [SerializeField] private Inventory inventory;

        private void Update()
        {
            healthBar.fillAmount = 1.0f * MechaController.Instance.currentHp / MechaController.Instance.maxHp;
            hpText.text = MechaController.Instance.currentHp + "/" + MechaController.Instance.maxHp;

            BonusPart part = inventory.equippedBonusPart;
            boostBar.gameObject.SetActive(part != null && part is BoostPart);
            boostText.gameObject.SetActive(part != null && part is BoostPart);

            if (boostBar.IsActive() && boostText.IsActive())
            {
                boostBar.fillAmount = MechaController.Instance.currentBoost / MechaController.Instance.maxBoost;
                boostText.text = Mathf.Round(boostBar.fillAmount * 100 * 10.0f) * 0.1f + "%";
            }
            
            int currentWeight = MechaController.Instance.currentWeight;
            int medianWeight = MechaController.Instance.GetMedianWeight();
            weightText.text = currentWeight + "/" + medianWeight + " KG";
            weightText.color = currentWeight <= medianWeight ? Color.white : Color.red;

            speedText.text = Mathf.RoundToInt(MechaController.Instance.currentSpeed) + " KPH";

        }
    }
}
