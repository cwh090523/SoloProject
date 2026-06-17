using System;
using Unity.AppUI.MVVM;
using Unity.VisualScripting;
using UnityEngine;

public class ShopOwnerBubble : MonoBehaviour
{
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private GameObject bubbleRoot;

        private void Awake()
        {
                if(stateManager == null)
                        stateManager = FindFirstObjectByType<GameStateManager>();

                Refresh();
        }

        private void OnEnable()
        {
                if(stateManager != null)
                        stateManager.StateChanged += HandleStateChanged;

                Refresh();
        }

        private void OnDisable()
        {
                if(stateManager != null)
                        stateManager.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
               Refresh();
        }

        private void Refresh()
        {
                if (bubbleRoot == null)
                        return;

                bool shouldShow = stateManager != null && stateManager.CurrentState == GameState.Restock;
                bubbleRoot.SetActive(shouldShow);
        }
}