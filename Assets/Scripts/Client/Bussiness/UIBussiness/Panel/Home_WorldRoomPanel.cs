using UnityEngine;
using ZeroUIFrame;
using Game.Client.Bussiness.EventCenter;
using Game.Client.Bussiness.UIBussiness;
using UnityEngine.UI;

namespace Game.Bussiness.UIBussiness.Panel
{

    public class Home_WorldRoomPanel : UIBehavior
    {

        InputField roomNameInput;
        int roomIndex;

        void Awake()
        {
            NetworkEventCenter.Regist_WorldRoomCreate(CreateRoomSuccess);

            roomNameInput = GetComponentFromChild<InputField>("CreateRoom/InputField");
        }

        void OnEnable()
        {
            SetOnClick("CreateRoom", ClickCreateRoomBtn);
        }

        // == Network ==
        void CreateRoomSuccess(string masterAccount, string roomName, string host, ushort port)
        {
            var go = UIManager.GetUIAsset("RoomItem");
            go = GameObject.Instantiate(go);
            string content = "RoomGroup/Viewport/Content";
            var parent = transform.Find(content);
            Debug.Assert(parent != null);
            go.transform.SetParent(parent, true);
            go.transform.name += roomIndex++;
            string path = $"{content}/{go.transform.name}";
            string info = $"房间名:{roomName} \n 房主:{masterAccount} ";
            Text_SetText(path + "/Info", info);
            SetOnClick(path, ClickRoom, host, port); //一个人限制创建一个房间

            SetActive("CreateRoom", false); //一个人限制创建一个房间
        }

        // == UI Click ==
        void ClickCreateRoomBtn(params object[] args)
        {
            UIEventCenter.WorldRoomCreateAction.Invoke(roomNameInput.text);
        }

        void ClickRoom(params object[] args)
        {
            //点击进入
            string host = (string)args[0];
            ushort port = (ushort)args[1];

            UIEventCenter.WorldRoomEnter.Invoke(host, port);
            UIEventCenter.EnqueueTearDownQueue("Home_WorldRoomPanel");
        }

    }

}