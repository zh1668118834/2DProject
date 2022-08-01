using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Game.Generic;
using Game.Facades;

namespace Game.Manager
{

    public class UIMgr
    {
        public static void Init()
        {
            int layer = LayerMask.NameToLayer("UI");
            _uiRoot = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler));
            _uiRootCanvas = _uiRoot.GetComponent<Canvas>();
            _uiRootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            _uiRootCanvas.sortingLayerID = SortingLayer.NameToID("Billboard");
            _uiRoot.layer = layer;
            _uiRootRt = _uiRoot.GetComponent<RectTransform>();
            GameObject.DontDestroyOnLoad(_uiRoot);
            CanvasScaler tempScale = _uiRoot.GetComponent<CanvasScaler>();
            tempScale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            tempScale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            tempScale.referenceResolution = UIDef.UIResolution;
            _uiRootRt.position = Vector3.zero;//CanvasScaler 会进行重置坐标 等其加载完成
            CameraMgr.UICamTrans.SetParent(_uiRoot.transform, false);


            _uiMainView = new GameObject("UIMainView", typeof(RectTransform)).GetComponent<RectTransform>();
            _SetRectTransform(ref _uiMainView);
            _uiMainView.transform.SetParent(_uiRoot.transform, false);

            foreach (var item in SortingLayer.layers)
            {
                if (item.value == 0) continue;
                var layerGO = new GameObject(item.name, typeof(Canvas));
                var layerRct = layerGO.GetComponent<RectTransform>();
                layerRct.transform.SetParent(_uiMainView.transform, false);
                layerRct.gameObject.layer = layer;
                Canvas canvas = layerGO.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingLayerID = item.id;
                canvas.sortingOrder = _curSortingOrder;
                _layerSortingDic.Add(item.name, new int2(_curSortingOrder, 0));
                _curSortingOrder += 10;
                _SetRectTransform(ref layerRct);
                _uiLayerDic.Add(item.name, layerRct.transform);
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule), typeof(BaseInput));
            eventSystem.transform.SetParent(_uiRoot.transform, false);
        }

        public static bool IsActive(string uiName)
        {
            Transform ui = null;
            return _CheckUI(uiName, ref ui) && ui.gameObject.activeInHierarchy;
        }

        public static void OpenUI(string uiName, params object[] args)
        {
            Transform ui = null;
            if (!_CheckUI(uiName, ref ui))
            {
                if (!_TryCreateUI(uiName, ref ui))
                {
                    Debug.LogError(string.Format("UI: {0} 不存在！", uiName));
                    return;
                }
                else
                {
                    Debug.Log(new StringBuilder("UI: ").Append(uiName).Append(" Loaded------"));
                }
            }

            if (ui.gameObject.activeInHierarchy) return;

            Canvas canvas;
            if (!ui.GetComponent<Canvas>()) canvas = ui.gameObject.AddComponent<Canvas>();
            else canvas = ui.GetComponent<Canvas>();
            string layer = uiName.Split('_')[0];
            int2 value = _layerSortingDic[layer];

            if (!_uiDic.ContainsKey(uiName))
            {
                _uiDic.Add(uiName, ui);
                canvas.overrideSorting = true;
                canvas.sortingLayerID = SortingLayer.NameToID(layer);
            }
            else
            {
                Debug.Log(new StringBuilder("UI: ").Append(uiName).Append(" ReOpened---"));
            }

            // TODO: 传参args
            value.y++;
            canvas.sortingOrder = value.x + value.y;
            ui.gameObject.SetActive(true);
            _layerSortingDic[layer] = value;
        }

        public static void CloseUI(string uiName)
        {
            Transform ui = null;
            if (!_CheckUI(uiName, ref ui))
            {
                return;
            }
            if (!ui.gameObject.activeInHierarchy)
            {
                return;
            }

            Canvas canvas = ui.GetComponent<Canvas>();
            string layer = uiName.Split('_')[0];
            Transform layerTrans = _uiLayerDic[layer];
            Canvas[] allCanvas = layerTrans.GetComponentsInChildren<Canvas>();

            // Update Layer Sorting
            for (int i = 1; i < allCanvas.Length; i++)
            {
                Canvas otherCanvas = allCanvas[i];
                if (otherCanvas.sortingOrder > canvas.sortingOrder)
                {
                    otherCanvas.sortingOrder--;
                }
            }

            int2 value = _layerSortingDic[layer];
            value.y--;
            _layerSortingDic[layer] = value;
            ui.gameObject.SetActive(false);
        }

        public static void DestoryUI(string uiName)
        {
            Transform ui = null;
            if (!_CheckUI(uiName, ref ui))
            {
                return;
            }
            GameObject.Destroy(ui.gameObject);
            string layer = uiName.Split('_')[0];
            int2 value = _layerSortingDic[layer];
            value.y--;
            _layerSortingDic[layer] = value;
        }

        private static bool _TryCreateUI(string uiName, ref Transform ui)
        {
            GameObject go = AllAssets.UIASSETS.Get(uiName);
            if (!go) return false;

            go.SetActive(false);
            go = GameObject.Instantiate(go);
            ui = go.transform;
            ui.name = uiName;

            if (ui.GetComponent<GraphicRaycaster>() == null)
            {
                ui.gameObject.AddComponent<GraphicRaycaster>();
            }

            string layer = uiName.Split('_')[0];
            ui.SetParent(_uiLayerDic[layer], false);
            return true;
        }

        private static bool _CheckUI(string uiName, ref Transform ui)
        {
            if (UIMgr._uiDic.ContainsKey(uiName))
            {
                ui = UIMgr._uiDic[uiName];
                return true;
            }

            return false;
        }

        private static void _SetRectTransform(ref RectTransform rct)
        {
            rct.pivot = new Vector2(0.5f, 0.5f);
            rct.anchorMin = Vector2.zero;
            rct.anchorMax = Vector2.one;
            rct.offsetMax = Vector2.zero;
            rct.offsetMin = Vector2.zero;
        }

        private static GameObject _uiRoot;
        private static Canvas _uiRootCanvas;
        private static RectTransform _uiRootRt;
        private static GameObject _worldUIRoot;
        private static Canvas _worldUIRootCanvas;
        private static RectTransform _worldUIRootRt;
        private static RectTransform _uiMainView;
        private static Dictionary<string, Transform> _uiLayerDic = new Dictionary<string, Transform>();
        private static Dictionary<string, Transform> _uiDic = new Dictionary<string, Transform>();
        private static int _curSortingOrder = 0;
        private static Dictionary<string, int2> _layerSortingDic = new Dictionary<string, int2>();

    }

}

