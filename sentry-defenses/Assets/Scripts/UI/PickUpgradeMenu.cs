using System;
using DG.Tweening;
using Game;
using UnityEngine;
using UnityEngine.UI;

public class PickUpgradeMenu : MonoBehaviour
{
    [SerializeField] private GameObject _newTowerPrefab;
    [SerializeField] private GameObject _upgradeDamagePrefab;
    [SerializeField] private GameObject _upgradeFireRatePrefab;
    [SerializeField] private GameObject _upgradeRangePrefab;
    
    [Header("Container")] 
    [SerializeField] private RectTransform _topContainer;
    [SerializeField] private RectTransform _bottomContainer;
    
    [Header("Movement")] 
    [SerializeField] private float _moveDuration = 0.3f;
    [SerializeField] private Ease _showEase = Ease.OutBack;
    [SerializeField] private Ease _hideEase = Ease.InBack;
    
    public void CreateButton(UpgradeType upgradeType, Action<UpgradeType> clickedCallback)
    {
        var prefab = upgradeType switch
        {
            UpgradeType.Range => _upgradeRangePrefab,
            UpgradeType.FireRate => _upgradeFireRatePrefab,
            UpgradeType.Damage => _upgradeDamagePrefab,
            UpgradeType.NewTower => _newTowerPrefab,
            _ => null
        };

        var button = Instantiate(prefab, _bottomContainer, true);
        button.GetComponent<RectTransform>().localScale = Vector3.one;
        button.GetComponent<Button>().onClick.AddListener(() => clickedCallback.Invoke(upgradeType));
    }
    
    public void Show(Action finishedCallback)
    {
        _topContainer.DOAnchorPosY(0, _moveDuration).SetEase(_showEase);
        _bottomContainer.DOAnchorPosX(0, _moveDuration).SetEase(_showEase).OnComplete(finishedCallback.Invoke);
    }

    public void Hide(Action finishCallback)
    {
        _topContainer.DOAnchorPosY(_topContainer.rect.height, _moveDuration).SetEase(_hideEase);
        _bottomContainer.DOAnchorPosX(-_bottomContainer.rect.width, _moveDuration)
            .SetEase(_hideEase)
            .OnStepComplete(() =>
            {
                ResetButtons();
                finishCallback?.Invoke();
            });
    }

    public void Prepare()
    {
        _topContainer.anchoredPosition = new Vector2(0, _topContainer.rect.height);
        _bottomContainer.anchoredPosition = new Vector2(-_bottomContainer.rect.width, 0);
        ResetButtons();
    }

    private void ResetButtons()
    {
        foreach (Transform child in _bottomContainer.transform)
        {
            Destroy(child.gameObject);
        }
    }
}
