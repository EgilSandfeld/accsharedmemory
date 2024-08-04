using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Sim.AssettoCorsaCompetizione;

[JsonConverter(typeof(StringEnumConverter))]
public enum OutboundMessageTypes : byte
{
    REGISTER_COMMAND_APPLICATION = 1,
    UNREGISTER_COMMAND_APPLICATION = 9,
    REQUEST_ENTRY_LIST = 10,
    REQUEST_TRACK_DATA = 11,
    CHANGE_HUD_PAGE = 49,
    CHANGE_FOCUS = 50,
    INSTANT_REPLAY_REQUEST = 51,
    PLAY_MANUAL_REPLAY_HIGHLIGHT = 52,
    SAVE_MANUAL_REPLAY_HIGHLIGHT = 60
}

[JsonConverter(typeof(StringEnumConverter))]
public enum InboundMessageTypes : byte
{
    REGISTRATION_RESULT = 1,
    REALTIME_UPDATE = 2,
    REALTIME_CAR_UPDATE = 3,
    ENTRY_LIST = 4,
    ENTRY_LIST_CAR = 6,
    TRACK_DATA = 5,
    BROADCASTING_EVENT = 7
}

[JsonConverter(typeof(StringEnumConverter))]
public enum LapType
{
    Regular,
    Outlap,
    Inlap
}

[JsonConverter(typeof(StringEnumConverter))]
public enum DriverCategory
{
    Bronze,
    Silver,
    Gold,
    Platinum,
    Unknown = 255
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CarLocation
{
    Unknown,
    Track,
    Pitlane,
    PitEntry,
    PitExit
}

[JsonConverter(typeof(StringEnumConverter))]
public enum SessionPhase
{
    Unknown,
    Starting,
    PreFormation,
    FormationLap,
    PreSession,
    Session, //Racing
    SessionOver,
    PostSession,
    ResultUI
}

[JsonConverter(typeof(StringEnumConverter))]
public enum SessionType
{
    Practice,
    Qualifying,
    Superpole,
    Race,
    Hotlap,
    HotStint,
    HotlapSuperpole,
    Replay
}

[JsonConverter(typeof(StringEnumConverter))]
public enum BroadcastingEventType
{
    Unknown,
    GreenFlag,
    SessionOver,
    PenaltyCommunicationMessage,
    Accident,
    LapCompleted,
    BestSessionLap,
    BestPersonalLap
}

[JsonConverter(typeof(StringEnumConverter))]
public enum Nationality
{
    Unknown,
    Italy,
    Germany,
    France,
    Spain,
    GreatBritain,
    Hungary,
    Belgium,
    Switzerland,
    Austria,
    Russia,
    Thailand,
    Netherlands,
    Poland,
    Argentina,
    Monaco,
    Ireland,
    Brazil,
    SouthAfrica,
    PuertoRico,
    Slovakia,
    Oman,
    Greece,
    SaudiArabia,
    Norway,
    Turkey,
    SouthKorea,
    Lebanon,
    Armenia,
    Mexico,
    Sweden,
    Finland,
    Denmark,
    Croatia,
    Canada,
    China,
    Portugal,
    Singapore,
    Indonesia,
    USA,
    NewZealand,
    Australia,
    SanMarino,
    UAE,
    Luxembourg,
    Kuwait,
    HongKong,
    Colombia,
    Japan,
    Andorra,
    Azerbaijan,
    Bulgaria,
    Cuba,
    CzechRepublic,
    Estonia,
    Georgia,
    India,
    Israel,
    Jamaica,
    Latvia,
    Lithuania,
    Macau,
    Malaysia,
    Nepal,
    NewCaledonia,
    Nigeria,
    NorthernIreland,
    PapuaNewGuinea,
    Philippines,
    Qatar,
    Romania,
    Scotland,
    Serbia,
    Slovenia,
    Taiwan,
    Ukraine,
    Venezuela,
    Wales
}