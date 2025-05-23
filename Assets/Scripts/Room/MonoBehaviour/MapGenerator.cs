using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("地图配置表")]
    public MapConfigSO mapConfig;
    [Header("地图布局")]
    public MapLayoutSO mapLayout;
    [Header("预制体")]
    public Room roomPrefab;
    public LineRenderer linePrefab;

    private float screenHeight;
    private float screenWidth;
    private float columnWidth;
    private Vector3 generatePoint;
    public float border;
    private List<Room> rooms = new List<Room>();
    private List<LineRenderer> lines = new List<LineRenderer>();
    public List<RoomDataSO> roomDataList = new List<RoomDataSO>();
    private Dictionary<RoomType, RoomDataSO> roomDataDict = new Dictionary<RoomType, RoomDataSO>();

    private void Awake()
    {
        // 获取屏幕的高度和宽度
        screenHeight = Camera.main.orthographicSize * 2;
        screenWidth = screenHeight * Camera.main.aspect;

        // 获取一列的宽度
        columnWidth = screenWidth / mapConfig.roomBlueprints.Count;

        foreach (var roomData in roomDataList)
        {
            roomDataDict.Add(roomData.roomType, roomData);
        }
    }

    private void Start() {
        // 每次启动的时候，从文件中加载 MapLayoutSO
        DataManager.instance.LoadFromFile(mapLayout);
    }

    private void OnEnable() {
        if (mapLayout.mapRoomDataList.Count > 0)
        {
            LoadMap();
        }
        else
        {
            CreateMap();
        }
    }

    public void CreateMap()
    {
        // 创建前一列房间列表
        List<Room> previousColumnRooms = new List<Room>();

        for (int column = 0; column < mapConfig.roomBlueprints.Count; column++)
        {
            var blueprint = mapConfig.roomBlueprints[column];

            var amount = UnityEngine.Random.Range(blueprint.min, blueprint.max + 1);

            var startHeight = screenHeight / 2 - screenHeight / (amount + 1);

            generatePoint = new Vector3(-screenWidth / 2 + border + columnWidth * column, startHeight, 0);

            var newPosition = generatePoint;
            
            // 创建当前列房间列表
            List<Room> currentColumnRooms = new List<Room>();
            
            var roomGapY = screenHeight / (amount + 1);

            // 循环当前列的所有房间数量生成房间
            for (int i = 0; i < amount; i++)
            {
                if (column == mapConfig.roomBlueprints.Count - 1)
                {
                    // 判断为最后一列，Boss 房间
                    newPosition.x = screenWidth / 2 - border * 2;
                }
                else if (column != 0)
                {
                    newPosition.x = generatePoint.x + UnityEngine.Random.Range(-border / 2, border);
                }                 

                newPosition.y = startHeight - roomGapY * i;
                // 生成房间
                var room = Instantiate(roomPrefab, newPosition, Quaternion.identity, transform);
                RoomType newType = GetRandomRoomType(mapConfig.roomBlueprints[column].roomType);

                // 设置只有第1列房间可以进入其他房间
                if (column == 0)
                {
                    room.roomState = RoomState.Attainable;
                }
                else
                {
                    room.roomState = RoomState.Locked;
                }

                room.SetupRoom(column, i, GetRoomData(newType));
                rooms.Add(room);

                currentColumnRooms.Add(room);
            }

            // 判断当前列是否为第一列，如果不是则连接到上一列
            if (previousColumnRooms.Count > 0)
            {
                // 创建两个列表的房间连线
                CreateConnection(previousColumnRooms, currentColumnRooms);
            }

            previousColumnRooms = currentColumnRooms;
        }
    
        SaveMap();
    }

    private void CreateConnection(List<Room> column1, List<Room> column2)
    {
        HashSet<Room> connectedColumn2Rooms = new HashSet<Room>();
        
        foreach (var room in column1)
        {
            var targetRoom = ConnectToRandomRoom(room, column2, false);
            connectedColumn2Rooms.Add(targetRoom);
        }

        // 检查确保 Column2 中所有房间都有连接的房间
        foreach (var room in column2)
        {
            if (!connectedColumn2Rooms.Contains(room))
            {
                ConnectToRandomRoom(room, column1, true);
            }
        }
    }

    /// <summary>
    /// 将 room 与 column2 上一个随机的房间进行相连
    /// </summary>
    /// <param name="room">需要连线的房间</param>
    /// <param name="column2">需要被连接的房间列表</param>
    /// <param name="check">如果是后面的房间向前连接则为true，如果是前面的房间向后连接则为false</param>
    /// <returns></returns>
    private Room ConnectToRandomRoom(Room room, List<Room> column2, bool check)
    {
        Room targetRoom;

        targetRoom = column2[UnityEngine.Random.Range(0, column2.Count)];

        if (check)
        {
            // 说明是后面的房间向前连接
            targetRoom.linkTo.Add(new Vector2Int(room.column, room.line));
        }
        else
        {
            // 说明是前面的房间向后连接
            room.linkTo.Add(new Vector2Int(targetRoom.column, targetRoom.line));
        }

        // 创建房间之间的连线
        var line = Instantiate(linePrefab, transform);
        // 要确保一下连线的方向是正确的
        if (check)
        {
            // 说明是后面的房间向前连接
            line.SetPosition(0, targetRoom.transform.position);
            line.SetPosition(1, room.transform.position);
        }
        else
        {
            // 说明是前面的房间向后连接
            line.SetPosition(0, room.transform.position);
            line.SetPosition(1, targetRoom.transform.position);
        }
        lines.Add(line);

        return targetRoom;
    }

    // 重新生成地图
    [ContextMenu("ReGenerateRoom")]
    public void ReGenerateRoom()
    {
        foreach (var room in rooms)
        {
            Destroy(room.gameObject);
        }

        foreach (var line in lines)
        {
            Destroy(line.gameObject);
        }

        rooms.Clear();
        lines.Clear();

        CreateMap();
    }

    private RoomDataSO GetRoomData(RoomType roomType)
    {
        return roomDataDict[roomType];
    }

    private RoomType GetRandomRoomType(RoomType flags)
    {
        // 首先获取出所有房间的名称字符串数组
        string[] options = flags.ToString().Split(',');

        // 然后从数组中随机取出一个
        string randomOption = options[UnityEngine.Random.Range(0, options.Length)];

        // 然后根据字符串获取到对应的 RoomType
        return (RoomType)Enum.Parse(typeof(RoomType), randomOption);
    }

    private void SaveMap()
    {
        mapLayout.mapRoomDataList = new List<MapRoomData>();
        // 添加所有已经生成的房间
        for (int i = 0; i < rooms.Count; i++)
        {
            var room = new MapRoomData()
            {
                posX = rooms[i].transform.position.x,
                posY = rooms[i].transform.position.y,
                column = rooms[i].column,
                line = rooms[i].line,
                roomType = rooms[i].roomData.roomType,
                roomState = rooms[i].roomState,
                linkTo = rooms[i].linkTo
            };

            mapLayout.mapRoomDataList.Add(room);
        }

        mapLayout.linePositionList = new List<LinePosition>();
        // 添加所有连线
        for (int i = 0; i < lines.Count; i++)
        {
            var line = new LinePosition()
            {
                startPos = new SerializeVector3(lines[i].GetPosition(0)),
                endPos = new SerializeVector3(lines[i].GetPosition(1))
            };

            mapLayout.linePositionList.Add(line);
        }

        // 存储到文件中
        DataManager.instance.SaveToFile(mapLayout);
    }

    private void LoadMap()
    {
        // 读取房间数据生成房间
        for (int i = 0; i < mapLayout.mapRoomDataList.Count; i++)
        {
            var newPos = new Vector3(mapLayout.mapRoomDataList[i].posX, mapLayout.mapRoomDataList[i].posY, 0);
            var newRoom = Instantiate(roomPrefab, newPos, Quaternion.identity, transform);
            newRoom.roomState = mapLayout.mapRoomDataList[i].roomState;
            RoomDataSO roomDataSO = roomDataDict[mapLayout.mapRoomDataList[i].roomType];
            newRoom.SetupRoom(mapLayout.mapRoomDataList[i].column, mapLayout.mapRoomDataList[i].line, roomDataSO);
            newRoom.linkTo = mapLayout.mapRoomDataList[i].linkTo;
            rooms.Add(newRoom);
        }

        // 读取连线
        for (int i = 0; i < mapLayout.linePositionList.Count; i++)
        {
            var line = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, transform);
            line.SetPosition(0, mapLayout.linePositionList[i].startPos.ToVector3());
            line.SetPosition(1, mapLayout.linePositionList[i].endPos.ToVector3());

            lines.Add(line);
        }
    }
}
