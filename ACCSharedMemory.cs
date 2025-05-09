using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Serilog;

namespace AssettoCorsaSharedMemory
{
    public delegate void PhysicsUpdatedHandler(object sender, PhysicsEventArgs e);

    public delegate void GraphicsUpdatedHandler(object sender, GraphicsEventArgs e);
    public delegate void EvoGraphicsUpdatedHandler(object sender, EvoGraphicsEventArgs e);

    public delegate void StaticInfoUpdatedHandler(object sender, StaticInfoEventArgs e);

    public delegate void MemoryStatusChangedHandler(object sender, MemoryStatusEventArgs e);

    public delegate void GameStatusChangedHandler(object sender, GameStatusEventArgs e);
    public delegate void EvoGameStatusChangedHandler(object sender, EvoGameStatusEventArgs e);

    public delegate void PitStatusChangedHandler(object sender, PitStatusEventArgs e);

    public delegate void SessionTypeChangedHandler(object sender, SessionTypeEventArgs e);

    public class AssettoCorsaNotStartedException : Exception
    {
        public AssettoCorsaNotStartedException()
            : base("Shared Memory not connected, is Assetto Corsa running and have you run assettoCorsa.Start()?")
        {
        }
    }

    public enum AC_MEMORY_STATUS
    {
        DISCONNECTED,
        CONNECTING,
        CONNECTED
    }

    public class ACCSharedMemory
    {
        public static ACCSharedMemory Instance;
        private Timer sharedMemoryRetryTimer;
        private AC_MEMORY_STATUS memoryStatus = AC_MEMORY_STATUS.DISCONNECTED;
        public bool IsConnected => (memoryStatus == AC_MEMORY_STATUS.CONNECTED);
        public bool IsRunning => (memoryStatus is AC_MEMORY_STATUS.CONNECTING || memoryStatus is AC_MEMORY_STATUS.CONNECTED);

        private ACCSharedMemoryGraphics _accSharedMemoryGraphics;

        private AC_STATUS gameStatus = AC_STATUS.AC_OFF;
        private int pitStatus = 1;
        private AC_SESSION_TYPE sessionType = AC_SESSION_TYPE.AC_UNKNOWN;

        public static readonly Dictionary<CarModel, int> BrakeBiasOffset = new Dictionary<CarModel, int>()
        {
            { CarModel.aston_martin_v12_vantage_gt3, -7 },
            { CarModel.audi_r8_lms, -14 },
            { CarModel.bentley_continental_gt3_2016, -7 },
            { CarModel.bentley_continental_gt3_2018, -7 },
            { CarModel.bmw_m6_gt3, -15 },
            { CarModel.jaguar_g3, -7 },
            { CarModel.ferrari_488_gt3, -17 },
            { CarModel.honda_nsx_gt3, -14 },
            { CarModel.lamborghini_gallardo_rex, -14 },
            { CarModel.lamborghini_huracan_gt3, -14 },
            { CarModel.lamborghini_huracan_supertrofeo, -14 },
            { CarModel.lexus_rc_f_gt3, -14 },
            { CarModel.mclaren_650s_gt3, -17 },
            { CarModel.mercedes_amg_gt3, -14 },
            { CarModel.nissan_gt_r_nismo_gt3_2017, -15 },
            { CarModel.nissan_gt_r_nismo_gt3_2018, -15 },
            { CarModel.porsche_991_gt3_r, -21 },
            { CarModel.porsche_991ii_gt3_cup, -5 },
            { CarModel.amr_v8_vantage_gt3_2019, -7 },
            { CarModel.audi_r8_lms_evo_2019, -14 },
            { CarModel.honda_nsx_gt3_evo_2019, -14 },
            { CarModel.lamborghini_huracan_gt3_evo_2019, -14 },
            { CarModel.mclaren_720s_gt3_2019, -17 },
            { CarModel.porsche_991ii_gt3_r_2019, -21 },
            { CarModel.alpine_a110_gt4, -15 },
            { CarModel.amr_v8_vantage_gt4, -20 },
            { CarModel.audi_r8_lms_gt4, -15 },
            { CarModel.bmw_m4_gt4, -22 },
            { CarModel.chevrolet_camaro_gt4r, -18 },
            { CarModel.ginetta_g55_gt4, -18 },
            { CarModel.ktm_xbow_gt4, -20 },
            { CarModel.maserati_mc_gt4, -15 },
            { CarModel.mclaren_570s_gt4, -9 },
            { CarModel.mercedes_amg_gt4, -20 },
            { CarModel.porsche_718_cayman_gt4_mr, -20 },
            { CarModel.ferrari_488_gt3_evo_2020, -17 },
            { CarModel.mercedes_amg_gt3_evo_2020, -14 },
            { CarModel.bmw_m4_gt3, -14 },
            { CarModel.audi_r8_lms_gt3_evo_ii, -14 },
            { CarModel.bmw_m2_cs_racing, -17 },
            { CarModel.ferrari_488_challenge_evo, -13 },
            { CarModel.lamborghini_huracan_supertrofeo_evo2, -14 },
            { CarModel.porsche_911_gt3_cup_992, -5 },
            { CarModel.ferrari_296_gt3, -5 },
            { CarModel.porsche_992_gt3_r, -21 },
            { CarModel.lamborghini_huracan_gt3_evo2, -14 },
            { CarModel.mclaren_720s_gt3_evo_2023, -17 },
            //{ CarModel.porsche_935,  }, //Unsure
            //{ CarModel.porsche_911_gt2_rs_cs_evo,  }, //Unsure
            //{ CarModel.mercedes_amg_gt2,  }, //Unsure
            //{ CarModel.maserati_mc20_gt2,  }, //Unsure
            //{ CarModel.ktm_xbow_gt2,  }, //Unsure
            //{ CarModel.audi_r8_lms_gt2,  }, //Unsure
            //{ CarModel.ford_mustang_gt3,  } //Unsure
        };

        public static readonly Dictionary<CarModel, int> MaxRPM = new ()
        {
            { CarModel.aston_martin_v12_vantage_gt3, 7750 },
            { CarModel.audi_r8_lms, 8650 },
            { CarModel.bentley_continental_gt3_2016, 7500 },
            { CarModel.bentley_continental_gt3_2018, 7400 },
            { CarModel.bmw_m6_gt3, 7100 },
            { CarModel.jaguar_g3, 8750 },
            { CarModel.ferrari_488_gt3, 7300 },
            { CarModel.honda_nsx_gt3, 7500 },
            { CarModel.lamborghini_gallardo_rex, 8650 },
            { CarModel.lamborghini_huracan_gt3, 8650 },
            { CarModel.lamborghini_huracan_supertrofeo, 8650 },
            { CarModel.lexus_rc_f_gt3, 7750 },
            { CarModel.mclaren_650s_gt3, 7500 },
            { CarModel.mercedes_amg_gt3, 7900 },
            { CarModel.nissan_gt_r_nismo_gt3_2017, 7500 },
            { CarModel.nissan_gt_r_nismo_gt3_2018, 7500 },
            { CarModel.porsche_991_gt3_r, 9250 },
            { CarModel.porsche_991ii_gt3_cup, 8500 },
            { CarModel.amr_v8_vantage_gt3_2019, 7250 },
            { CarModel.audi_r8_lms_evo_2019, 8650 },
            { CarModel.honda_nsx_gt3_evo_2019, 7650 },
            { CarModel.lamborghini_huracan_gt3_evo_2019, 8650 },
            { CarModel.mclaren_720s_gt3_2019, 7700 },
            { CarModel.porsche_991ii_gt3_r_2019, 9250 },
            { CarModel.alpine_a110_gt4, 6450 },
            { CarModel.amr_v8_vantage_gt4, 7000 },
            { CarModel.audi_r8_lms_gt4, 8650 },
            { CarModel.bmw_m4_gt4, 7600 },
            { CarModel.chevrolet_camaro_gt4r, 7500 },
            { CarModel.ginetta_g55_gt4, 7200 },
            { CarModel.ktm_xbow_gt4, 6500 },
            { CarModel.maserati_mc_gt4, 7000 },
            { CarModel.mclaren_570s_gt4, 7600 },
            { CarModel.mercedes_amg_gt4, 7000 },
            { CarModel.porsche_718_cayman_gt4_mr, 7800 },
            { CarModel.ferrari_488_gt3_evo_2020, 7600 },
            { CarModel.mercedes_amg_gt3_evo_2020, 7600 },
            { CarModel.bmw_m4_gt3, 7000 },
            { CarModel.audi_r8_lms_gt3_evo_ii, 8650 },
            { CarModel.bmw_m2_cs_racing, 7520 },
            { CarModel.ferrari_488_challenge_evo, 8000 },
            { CarModel.lamborghini_huracan_supertrofeo_evo2, 8650 },
            { CarModel.porsche_911_gt3_cup_992, 8750 },
            { CarModel.ferrari_296_gt3, 8000 },
            { CarModel.porsche_992_gt3_r, 9250 },
            { CarModel.lamborghini_huracan_gt3_evo2, 8650 },
            { CarModel.mclaren_720s_gt3_evo_2023, 7700 },
            { CarModel.porsche_935, 8000 }, //Unsure
            { CarModel.porsche_911_gt2_rs_cs_evo, 8750 }, //Unsure
            { CarModel.mercedes_amg_gt2, 8000 }, //Unsure
            { CarModel.maserati_mc20_gt2, 7700 }, //Unsure
            { CarModel.ktm_xbow_gt2, 7000 }, //Unsure
            { CarModel.audi_r8_lms_gt2, 8300 }, //Unsure
            { CarModel.ford_mustang_gt3, 8300 } //Unsure
        };

        public enum CarModel
        {
            Unknown = -1,
            porsche_991_gt3_r = 0,
            mercedes_amg_gt3 = 1,
            ferrari_488_gt3 = 2,
            audi_r8_lms = 3,
            lamborghini_huracan_gt3 = 4,
            mclaren_650s_gt3 = 5,
            nissan_gt_r_nismo_gt3_2018 = 6,
            bmw_m6_gt3 = 7,
            bentley_continental_gt3_2018 = 8,
            porsche_991ii_gt3_cup = 9,
            nissan_gt_r_nismo_gt3_2017 = 10,
            bentley_continental_gt3_2016 = 11,
            aston_martin_v12_vantage_gt3 = 12,
            lamborghini_gallardo_rex = 13,
            jaguar_g3 = 14,
            lexus_rc_f_gt3 = 15,
            lamborghini_huracan_gt3_evo_2019 = 16,
            honda_nsx_gt3 = 17,
            lamborghini_huracan_supertrofeo = 18,
            audi_r8_lms_evo_2019 = 19,
            amr_v8_vantage_gt3_2019 = 20,
            honda_nsx_gt3_evo_2019 = 21,
            mclaren_720s_gt3_2019 = 22,
            porsche_991ii_gt3_r_2019 = 23,
            ferrari_488_gt3_evo_2020 = 24,
            mercedes_amg_gt3_evo_2020 = 25,
            ferrari_488_challenge_evo = 26,
            bmw_m2_cs_racing = 27,
            porsche_911_gt3_cup_992 = 28,
            lamborghini_huracan_supertrofeo_evo2 = 29,
            bmw_m4_gt3 = 30,
            audi_r8_lms_gt3_evo_ii = 31,
            ferrari_296_gt3 = 32,
            lamborghini_huracan_gt3_evo2 = 33,
            porsche_992_gt3_r = 34,
            mclaren_720s_gt3_evo_2023 = 35,
            ford_mustang_gt3 = 36,
            alpine_a110_gt4 = 50,
            amr_v8_vantage_gt4 = 51,
            audi_r8_lms_gt4 = 52,
            bmw_m4_gt4 = 53,
            chevrolet_camaro_gt4r = 55,
            ginetta_g55_gt4 = 56,
            ktm_xbow_gt4 = 57,
            maserati_mc_gt4 = 58,
            mclaren_570s_gt4 = 59,
            mercedes_amg_gt4 = 60,
            porsche_718_cayman_gt4_mr = 61,
            audi_r8_lms_gt2 = 80,
            ktm_xbow_gt2 = 82,
            maserati_mc20_gt2 = 83,
            mercedes_amg_gt2 = 84,
            porsche_911_gt2_rs_cs_evo = 85,
            porsche_935 = 86,
            unknown_1 = 100,
            unknown_2 = 101,
            unknown_3 = 102,
            unknown_4 = 103
        }

        public static readonly Dictionary<CarModel, string> CarModelShortNames = new()
        {
            { CarModel.porsche_991_gt3_r, "991" },
            { CarModel.mercedes_amg_gt3, "AMG" },
            { CarModel.ferrari_488_gt3, "488" },
            { CarModel.audi_r8_lms, "R8" },
            { CarModel.lamborghini_huracan_gt3, "Huracan" },
            { CarModel.mclaren_650s_gt3, "650S" },
            { CarModel.nissan_gt_r_nismo_gt3_2018, "Nismo" },
            { CarModel.bmw_m6_gt3, "M6" },
            { CarModel.bentley_continental_gt3_2018, "Bentley" },
            { CarModel.porsche_991ii_gt3_cup, "991.2" },
            { CarModel.nissan_gt_r_nismo_gt3_2017, "Nismo" },
            { CarModel.bentley_continental_gt3_2016, "Bentley" },
            { CarModel.aston_martin_v12_vantage_gt3, "Vantage" },
            { CarModel.lamborghini_gallardo_rex, "Gallardo" },
            { CarModel.jaguar_g3, "Jag" },
            { CarModel.lexus_rc_f_gt3, "RCF" },
            { CarModel.lamborghini_huracan_gt3_evo_2019, "Huracan Evo" },
            { CarModel.honda_nsx_gt3, "NSX" },
            { CarModel.lamborghini_huracan_supertrofeo, "Huracan ST" },
            { CarModel.audi_r8_lms_evo_2019, "R8" },
            { CarModel.amr_v8_vantage_gt3_2019, "Vantage" },
            { CarModel.honda_nsx_gt3_evo_2019, "NSX" },
            { CarModel.mclaren_720s_gt3_2019, "720S" },
            { CarModel.porsche_991ii_gt3_r_2019, "991.2" },
            { CarModel.ferrari_488_gt3_evo_2020, "488" },
            { CarModel.mercedes_amg_gt3_evo_2020, "AMG" },
            { CarModel.ferrari_488_challenge_evo, "488" },
            { CarModel.bmw_m2_cs_racing, "M2" },
            { CarModel.porsche_911_gt3_cup_992, "992" },
            { CarModel.lamborghini_huracan_supertrofeo_evo2, "Huracan ST2" },
            { CarModel.bmw_m4_gt3, "M4" },
            { CarModel.audi_r8_lms_gt3_evo_ii, "R8" },
            { CarModel.ferrari_296_gt3, "296" },
            { CarModel.lamborghini_huracan_gt3_evo2, "Huracan" },
            { CarModel.porsche_992_gt3_r, "992" },
            { CarModel.mclaren_720s_gt3_evo_2023, "720S" },
            { CarModel.ford_mustang_gt3, "Stang" },
            { CarModel.alpine_a110_gt4, "A110" },
            { CarModel.amr_v8_vantage_gt4, "Vantage" },
            { CarModel.audi_r8_lms_gt4, "R8" },
            { CarModel.bmw_m4_gt4, "M4" },
            { CarModel.chevrolet_camaro_gt4r, "Camaro" },
            { CarModel.ginetta_g55_gt4, "G55" },
            { CarModel.ktm_xbow_gt4, "XBow" },
            { CarModel.maserati_mc_gt4, "MC" },
            { CarModel.mclaren_570s_gt4, "570S" },
            { CarModel.mercedes_amg_gt4, "AMG" },
            { CarModel.porsche_718_cayman_gt4_mr, "718" },
            { CarModel.audi_r8_lms_gt2, "R8" },
            { CarModel.ktm_xbow_gt2, "XBow" },
            { CarModel.maserati_mc20_gt2, "MC20" },
            { CarModel.mercedes_amg_gt2, "AMG" },
            { CarModel.porsche_911_gt2_rs_cs_evo, "911 RS" },
            { CarModel.porsche_935, "935" },
            { CarModel.Unknown, "?" },
            { CarModel.unknown_1, "?" },
            { CarModel.unknown_2, "?" },
            { CarModel.unknown_3, "?" },
            { CarModel.unknown_4, "?" }
        };

        public static readonly Dictionary<CarModel, string> CarModelNames = new()
        {
            { CarModel.porsche_991_gt3_r, "Porsche 991 GT3 R" },
            { CarModel.mercedes_amg_gt3, "Mercedes-AMG GT3" },
            { CarModel.ferrari_488_gt3, "Ferrari 488 GT3" },
            { CarModel.audi_r8_lms, "Audi R8 LMS" },
            { CarModel.lamborghini_huracan_gt3, "Lamborghini Huracan GT3" },
            { CarModel.mclaren_650s_gt3, "McLaren 650S GT3" },
            { CarModel.nissan_gt_r_nismo_gt3_2018, "Nissan GT-R Nismo GT3 2018" },
            { CarModel.bmw_m6_gt3, "BMW M6 GT3" },
            { CarModel.bentley_continental_gt3_2018, "Bentley Continental GT3 2018" },
            { CarModel.porsche_991ii_gt3_cup, "Porsche 991II GT3 Cup" },
            { CarModel.nissan_gt_r_nismo_gt3_2017, "Nissan GT-R Nismo GT3 2017" },
            { CarModel.bentley_continental_gt3_2016, "Bentley Continental GT3 2016" },
            { CarModel.aston_martin_v12_vantage_gt3, "Aston Martin V12 Vantage GT3" },
            { CarModel.lamborghini_gallardo_rex, "Lamborghini Gallardo R-EX" },
            { CarModel.jaguar_g3, "Jaguar G3" },
            { CarModel.lexus_rc_f_gt3, "Lexus RC F GT3" },
            { CarModel.lamborghini_huracan_gt3_evo_2019, "Lamborghini Huracan Evo (2019)" },
            { CarModel.honda_nsx_gt3, "Honda NSX GT3" },
            { CarModel.lamborghini_huracan_supertrofeo, "Lamborghini Huracan SuperTrofeo" },
            { CarModel.audi_r8_lms_evo_2019, "Audi R8 LMS Evo (2019)" },
            { CarModel.amr_v8_vantage_gt3_2019, "AMR V8 Vantage (2019)" },
            { CarModel.honda_nsx_gt3_evo_2019, "Honda NSX Evo (2019)" },
            { CarModel.mclaren_720s_gt3_2019, "McLaren 720S GT3 (2019)" },
            { CarModel.porsche_991ii_gt3_r_2019, "Porsche 911II GT3 R (2019)" },
            { CarModel.ferrari_488_gt3_evo_2020, "Ferrari 488 GT3 Evo 2020" },
            { CarModel.mercedes_amg_gt3_evo_2020, "Mercedes-AMG GT3 2020" },
            { CarModel.ferrari_488_challenge_evo, "Ferrari 488 Challenge Evo" },
            { CarModel.bmw_m2_cs_racing, "BMW M2 CS Racing" },
            { CarModel.porsche_911_gt3_cup_992, "Porsche 911 GT3 Cup (Type 992)" },
            { CarModel.lamborghini_huracan_supertrofeo_evo2, "Lamborghini Huracán Super Trofeo EVO2" },
            { CarModel.bmw_m4_gt3, "BMW M4 GT3" },
            { CarModel.audi_r8_lms_gt3_evo_ii, "Audi R8 LMS GT3 evo II" },
            { CarModel.ferrari_296_gt3, "Ferrari 296 GT3" },
            { CarModel.lamborghini_huracan_gt3_evo2, "Lamborghini Huracan Evo2" },
            { CarModel.porsche_992_gt3_r, "Porsche 992 GT3 R" },
            { CarModel.mclaren_720s_gt3_evo_2023, "McLaren 720S GT3 Evo 2023" },
            { CarModel.ford_mustang_gt3, "Ford Mustang GT3" },
            { CarModel.alpine_a110_gt4, "Alpine A110 GT4" },
            { CarModel.amr_v8_vantage_gt4, "AMR V8 Vantage GT4" },
            { CarModel.audi_r8_lms_gt4, "Audi R8 LMS GT4" },
            { CarModel.bmw_m4_gt4, "BMW M4 GT4" },
            { CarModel.chevrolet_camaro_gt4r, "Chevrolet Camaro GT4" },
            { CarModel.ginetta_g55_gt4, "Ginetta G55 GT4" },
            { CarModel.ktm_xbow_gt4, "KTM X-Bow GT4" },
            { CarModel.maserati_mc_gt4, "Maserati MC GT4" },
            { CarModel.mclaren_570s_gt4, "McLaren 570S GT4" },
            { CarModel.mercedes_amg_gt4, "Mercedes-AMG GT4" },
            { CarModel.porsche_718_cayman_gt4_mr, "Porsche 718 Cayman GT4" },
            { CarModel.audi_r8_lms_gt2, "Audi R8 LMS GT2" },
            { CarModel.ktm_xbow_gt2, "KTM XBOW GT2" },
            { CarModel.maserati_mc20_gt2, "Maserati MC20 GT2" },
            { CarModel.mercedes_amg_gt2, "Mercedes AMG GT2" },
            { CarModel.porsche_911_gt2_rs_cs_evo, "Porsche 911 GT2 RS CS Evo" },
            { CarModel.porsche_935, "Porsche 935" },
            { CarModel.Unknown, "Unknown" },
            { CarModel.unknown_1, "Unknown 1" },
            { CarModel.unknown_2, "Unknown 2" },
            { CarModel.unknown_3, "Unknown 3" },
            { CarModel.unknown_4, "Unknown 4" },
        };

        public static readonly Dictionary<CarModel, int> MaxSteeringAngleDegrees = new ()
        {
            { CarModel.aston_martin_v12_vantage_gt3, 320 },
            { CarModel.audi_r8_lms, 360 },
            { CarModel.bentley_continental_gt3_2016, 320 },
            { CarModel.bentley_continental_gt3_2018, 320 },
            { CarModel.bmw_m6_gt3, 283 },
            { CarModel.jaguar_g3, 360 },
            { CarModel.ferrari_488_gt3, 240 },
            { CarModel.honda_nsx_gt3, 310 },
            { CarModel.lamborghini_gallardo_rex, 360 },
            { CarModel.lamborghini_huracan_gt3, 310 },
            { CarModel.lamborghini_huracan_supertrofeo, 310 },
            { CarModel.lexus_rc_f_gt3, 320 },
            { CarModel.mclaren_650s_gt3, 240 },
            { CarModel.mercedes_amg_gt3, 320 },
            { CarModel.nissan_gt_r_nismo_gt3_2017, 320 },
            { CarModel.nissan_gt_r_nismo_gt3_2018, 320 },
            { CarModel.porsche_991_gt3_r, 400 },
            { CarModel.porsche_991ii_gt3_cup, 400 },
            { CarModel.amr_v8_vantage_gt3_2019, 320 },
            { CarModel.audi_r8_lms_evo_2019, 360 },
            { CarModel.honda_nsx_gt3_evo_2019, 310 },
            { CarModel.lamborghini_huracan_gt3_evo_2019, 310 },
            { CarModel.mclaren_720s_gt3_2019, 240 },
            { CarModel.porsche_991ii_gt3_r_2019, 400 },
            { CarModel.alpine_a110_gt4, 360 },
            { CarModel.amr_v8_vantage_gt4, 320 },
            { CarModel.audi_r8_lms_gt4, 360 },
            { CarModel.bmw_m4_gt4, 246 },
            { CarModel.chevrolet_camaro_gt4r, 360 },
            { CarModel.ginetta_g55_gt4, 360 },
            { CarModel.ktm_xbow_gt4, 290 },
            { CarModel.maserati_mc_gt4, 450 },
            { CarModel.mclaren_570s_gt4, 240 },
            { CarModel.mercedes_amg_gt4, 246 },
            { CarModel.porsche_718_cayman_gt4_mr, 400 },
            { CarModel.ferrari_488_gt3_evo_2020, 240 },
            { CarModel.mercedes_amg_gt3_evo_2020, 320 },
            { CarModel.bmw_m4_gt3, 270 },
            { CarModel.audi_r8_lms_gt3_evo_ii, 360 },
            { CarModel.bmw_m2_cs_racing, 180 },
            { CarModel.ferrari_488_challenge_evo, 240 },
            { CarModel.lamborghini_huracan_supertrofeo_evo2, 310 },
            { CarModel.porsche_911_gt3_cup_992, 270 },
            { CarModel.porsche_935, 720 }, 
            { CarModel.porsche_911_gt2_rs_cs_evo, 720 }, 
            { CarModel.mercedes_amg_gt2, 490 }, 
            { CarModel.maserati_mc20_gt2, 480 }, 
            { CarModel.ktm_xbow_gt2, 580 }, 
            { CarModel.audi_r8_lms_gt2, 720 }, 
            { CarModel.ford_mustang_gt3, 515 } 
        };

        public static readonly Dictionary<CarModel, Dimension> CarDimensionsMillimeters = new()
        {
            { CarModel.alpine_a110_gt4, new(4178, 1798) },
            { CarModel.aston_martin_v12_vantage_gt3, new(4760, 1979) },
            { CarModel.amr_v8_vantage_gt3_2019, new(4760, 2040) }, //Couldn't find it, found Aston Martin V12 Vantage GT3 2012 instead: https://gran-turismo.fandom.com/wiki/Aston_Martin_V12_Vantage_GT3_%2712
            { CarModel.amr_v8_vantage_gt4, new(4380, 1865) },
            { CarModel.audi_r8_lms_gt4, new(4467, 1940) },
            { CarModel.audi_r8_lms, new(4583, 1997) },
            { CarModel.audi_r8_lms_evo_2019, new(4599, 1997) },
            { CarModel.audi_r8_lms_gt3_evo_ii, new(4599, 1997) },
            { CarModel.bentley_continental_gt3_2016, new(4806, 1944) },
            { CarModel.bentley_continental_gt3_2018, new(4860, 2045) },
            { CarModel.bmw_m2_cs_racing, new(4461, 1990) },
            { CarModel.bmw_m4_gt3, new(5020, 2040) },
            { CarModel.bmw_m4_gt4, new(4863, 2093) },
            { CarModel.bmw_m6_gt3, new(4944, 2046) },
            { CarModel.chevrolet_camaro_gt4r, new(4783, 1897) },
            { CarModel.ferrari_296_gt3, new(4565, 1958) },
            { CarModel.ferrari_488_challenge_evo, new(4568, 1952) },
            { CarModel.ferrari_488_gt3, new(4633, 2045) },
            { CarModel.ferrari_488_gt3_evo_2020, new(4633, 2045) },
            { CarModel.ginetta_g55_gt4, new(4358, 1900) },
            { CarModel.honda_nsx_gt3, new(4612, 2040) },
            { CarModel.honda_nsx_gt3_evo_2019, new(4612, 2040) },
            { CarModel.jaguar_g3, new(4793, 1892) }, //Unsure, Emil Frey XKR G3 conversion from: https://en.wikipedia.org/wiki/Jaguar_XK_(X150)
            { CarModel.ktm_xbow_gt4, new(3738, 1900) },
            { CarModel.lamborghini_gallardo_rex, new(4300, 1920) },
            { CarModel.lamborghini_huracan_gt3, new(4458, 2050) },
            { CarModel.lamborghini_huracan_gt3_evo_2019, new(4551, 2221) },
            { CarModel.lamborghini_huracan_supertrofeo, new(4549, 1945) },
            { CarModel.lamborghini_huracan_supertrofeo_evo2, new(4551, 2221) },
            { CarModel.lexus_rc_f_gt3, new(4705, 2000) },
            { CarModel.maserati_mc_gt4, new(4930, 1920) }, //Unsure: https://fastestlaps.com/models/maserati-granturismo-mc-stradale
            { CarModel.mclaren_570s_gt4, new(4606, 2095) },
            { CarModel.mclaren_650s_gt3, new(4534, 2040) },
            { CarModel.mclaren_720s_gt3_2019, new(4664, 2040) },
            { CarModel.mclaren_720s_gt3_evo_2023, new(4543, 2161) }, //https://www.conceptcarz.com/s32741/mclaren-720s-gt3-evo.aspx
            { CarModel.mercedes_amg_gt3, new(4710, 1990) },
            { CarModel.mercedes_amg_gt3_evo_2020, new(4746, 2049) },
            { CarModel.mercedes_amg_gt4, new(4619, 1996) },
            { CarModel.nissan_gt_r_nismo_gt3_2017, new(4690, 1895) },
            { CarModel.nissan_gt_r_nismo_gt3_2018, new(4690, 1895) },
            { CarModel.porsche_718_cayman_gt4_mr, new(4456, 1778) },
            { CarModel.porsche_991_gt3_r, new(4604, 2002) }, //2018 https://www.stuttcars.com/porsche-911-gt3-r-991-2016-2018/
            { CarModel.porsche_991ii_gt3_cup, new(4564, 1980) },
            { CarModel.porsche_991ii_gt3_r_2019, new(4629, 2002) },
            { CarModel.porsche_935, new(4864, 2035) }, //https://www.conceptcarz.com/s28876/porsche-935.aspx
            { CarModel.porsche_911_gt2_rs_cs_evo, new(4743, 1978) }, //https://www.ultimatecarpage.com/spec/8484/Porsche-911-GT2-RS-Clubsport-Evo.html
            { CarModel.porsche_992_gt3_r, new(4619, 2050) }, //https://www.loveforporsche.com/technical-data-porsche-911-gt3-r-992-model-year-2023/
            { CarModel.mercedes_amg_gt2, new(4546, 2007) }, //Unsure, https://en.wikipedia.org/wiki/Mercedes-AMG_GT
            { CarModel.maserati_mc20_gt2, new(4838, 2030) }, //https://www.maserati.com/global/en/corse/gt2/gt2-technical-specifications
            { CarModel.ktm_xbow_gt2, new(4626, 2040) }, //https://www.ktm.com/en-dk/models/x-bow/x-bow-gt2-2020.html
            { CarModel.audi_r8_lms_gt2, new(4568, 1995) }, //https://www.audi.com/en/sport/motorsport/audi-sport-customer-racing/r8-lms-gt2.html
            { CarModel.ford_mustang_gt3, new(4923, 2000) } //https://gran-turismo.fandom.com/wiki/Ford_Mustang_Gr.3
        };

        public event MemoryStatusChangedHandler MemoryStatusChanged;

        public virtual void OnMemoryStatusChanged(MemoryStatusEventArgs e)
        {
            if (MemoryStatusChanged != null)
            {
                MemoryStatusChanged(this, e);
            }
        }

        /// <summary>
        /// Event raised when the SDK detects the sim for the first time.
        /// </summary>
        public event EventHandler Connected;

        private void OnConnected(EventArgs e)
        {
            var handler = Connected;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Event raised when the SDK no longer detects the sim (sim closed).
        /// </summary>
        public event EventHandler Disconnected;

        private void OnDisconnected(EventArgs e)
        {
            var handler = Disconnected;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Event raised when the SDK no longer detects the sdk header content (when sim is closed, using UI and the user registers and starts joining another server)
        /// </summary>
        public event EventHandler Connecting;

        private void OnConnecting(EventArgs e)
        {
            var handler = Connecting;
            handler?.Invoke(this, e);
        }

        public event GameStatusChangedHandler GameStatusChanged;

        public virtual void OnGameStatusChanged(GameStatusEventArgs e)
        {
            if (GameStatusChanged != null)
            {
                GameStatusChanged(this, e);
            }
        }


        public event PitStatusChangedHandler PitStatusChanged;

        public virtual void OnPitStatusChanged(PitStatusEventArgs e)
        {
            if (PitStatusChanged != null)
            {
                PitStatusChanged(this, e);
            }
        }

        public event SessionTypeChangedHandler SessionTypeChanged;

        public virtual void OnSessionTypeChangedHandler(PitStatusEventArgs e)
        {
            if (PitStatusChanged != null)
            {
                PitStatusChanged(this, e);
            }
        }

        public static readonly Dictionary<AC_STATUS, string> StatusNameLookup = new Dictionary<AC_STATUS, string>
        {
            { AC_STATUS.AC_OFF, "Off" },
            { AC_STATUS.AC_LIVE, "Live" },
            { AC_STATUS.AC_PAUSE, "Pause" },
            { AC_STATUS.AC_REPLAY, "Replay" },
        };

        public ACCSharedMemory(int telemetryUpdateIntervalMs)
        {
            Instance = this;

            sharedMemoryRetryTimer = new Timer(2000);
            sharedMemoryRetryTimer.AutoReset = true;
            sharedMemoryRetryTimer.Elapsed += sharedMemoryRetryTimer_Elapsed;

            physicsTimer = new Timer();
            physicsTimer.AutoReset = true;
            physicsTimer.Elapsed += physicsTimer_Elapsed;
            PhysicsInterval = telemetryUpdateIntervalMs;

            graphicsTimer = new Timer();
            graphicsTimer.AutoReset = true;
            graphicsTimer.Elapsed += graphicsTimer_Elapsed;
            GraphicsInterval = telemetryUpdateIntervalMs;

            staticInfoTimer = new Timer();
            staticInfoTimer.AutoReset = true;
            staticInfoTimer.Elapsed += staticInfoTimer_Elapsed;
            StaticInfoInterval = 1000;

            Stop();
        }

        /// <summary>
        /// Connect to the shared memory and start the update timers
        /// </summary>
        public void Start()
        {
            sharedMemoryRetryTimer.Start();
            Log.ForContext("Context", "Sim").Verbose("ACC SharedMemory Start");
        }

        void sharedMemoryRetryTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ConnectToSharedMemory();
        }

        private bool ConnectToSharedMemory()
        {
            try
            {
                SetMemoryStatus(AC_MEMORY_STATUS.CONNECTING);
                // Connect to shared memory
                physicsMMF = MemoryMappedFile.OpenExisting("Local\\acpmf_physics");
                graphicsMMF = MemoryMappedFile.OpenExisting("Local\\acpmf_graphics");
                staticInfoMMF = MemoryMappedFile.OpenExisting("Local\\acpmf_static");

                // Start the timers
                staticInfoTimer.Start();
                ProcessStaticInfo();

                graphicsTimer.Start();
                ProcessGraphics();

                physicsTimer.Start();
                ProcessPhysics();

                // Stop retry timer
                sharedMemoryRetryTimer.Stop();
                SetMemoryStatus(AC_MEMORY_STATUS.CONNECTED);
                return true;
            }
            catch (FileNotFoundException)
            {
                staticInfoTimer.Stop();
                graphicsTimer.Stop();
                physicsTimer.Stop();
                return false;
            }
        }

        private void SetMemoryStatus(AC_MEMORY_STATUS status)
        {
            lock (_memoryStatusLock)
            {
                if (memoryStatus == status)
                    return;

                memoryStatus = status;
            }

            var memoryStatusEventArgs = new MemoryStatusEventArgs(status);
            OnMemoryStatusChanged(memoryStatusEventArgs);

            if (status == AC_MEMORY_STATUS.CONNECTED)
                OnConnected(memoryStatusEventArgs);

            if (status == AC_MEMORY_STATUS.CONNECTING)
                OnConnecting(memoryStatusEventArgs);

            if (status == AC_MEMORY_STATUS.DISCONNECTED)
                OnDisconnected(memoryStatusEventArgs);
        }

        /// <summary>
        /// Stop the timers and dispose of the shared memory handles
        /// </summary>
        public void Stop()
        {
            SetMemoryStatus(AC_MEMORY_STATUS.DISCONNECTED);
            sharedMemoryRetryTimer.Stop();

            // Stop the timers
            physicsTimer.Stop();
            graphicsTimer.Stop();
            staticInfoTimer.Stop();

            staticInfoMMF?.Dispose();
            graphicsMMF?.Dispose();
            physicsMMF?.Dispose();
        }

        /// <summary>
        /// Interval for physics updates in milliseconds
        /// </summary>
        public double PhysicsInterval
        {
            get { return physicsTimer.Interval; }
            set { physicsTimer.Interval = value; }
        }

        /// <summary>
        /// Interval for graphics updates in milliseconds
        /// </summary>
        public double GraphicsInterval
        {
            get { return graphicsTimer.Interval; }
            set { graphicsTimer.Interval = value; }
        }

        /// <summary>
        /// Interval for static info updates in milliseconds
        /// </summary>
        public double StaticInfoInterval
        {
            get { return staticInfoTimer.Interval; }
            set { staticInfoTimer.Interval = value; }
        }

        MemoryMappedFile physicsMMF;
        MemoryMappedFile graphicsMMF;
        MemoryMappedFile staticInfoMMF;

        Timer physicsTimer;
        Timer graphicsTimer;
        Timer staticInfoTimer;
        private readonly object _memoryStatusLock = new();

        /// <summary>
        /// Represents the method that will handle the physics update events
        /// </summary>
        public event PhysicsUpdatedHandler PhysicsUpdated;

        /// <summary>
        /// Represents the method that will handle the graphics update events
        /// </summary>
        public event GraphicsUpdatedHandler GraphicsUpdated;

        /// <summary>
        /// Represents the method that will handle the static info update events
        /// </summary>
        public event StaticInfoUpdatedHandler StaticInfoUpdated;

        public virtual void OnPhysicsUpdated(PhysicsEventArgs e)
        {
            PhysicsUpdated?.Invoke(this, e);
        }

        public virtual void OnGraphicsUpdated(GraphicsEventArgs e)
        {
            if (GraphicsUpdated != null)
            {
                GraphicsUpdated(this, e);
                if (gameStatus != e.ACCSharedMemoryGraphics.Status)
                {
                    gameStatus = e.ACCSharedMemoryGraphics.Status;
                    GameStatusChanged?.Invoke(this, new GameStatusEventArgs(gameStatus));
                }

                if (pitStatus != e.ACCSharedMemoryGraphics.IsInPit)
                {
                    pitStatus = e.ACCSharedMemoryGraphics.IsInPit;
                    PitStatusChanged?.Invoke(this, new PitStatusEventArgs(pitStatus));
                }

                if (sessionType != e.ACCSharedMemoryGraphics.Session)
                {
                    sessionType = e.ACCSharedMemoryGraphics.Session;
                    SessionTypeChanged?.Invoke(this, new SessionTypeEventArgs(sessionType));
                }
            }
        }

        public virtual void OnStaticInfoUpdated(StaticInfoEventArgs e)
        {
            StaticInfoUpdated?.Invoke(this, e);
        }

        private void physicsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessPhysics();
        }

        private void graphicsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessGraphics();
        }

        private void staticInfoTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessStaticInfo();
        }

        private void ProcessPhysics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED)
                return;

            try
            {
                ACCSharedMemoryPhysics accSharedMemoryPhysics = ReadPhysics();
                OnPhysicsUpdated(new PhysicsEventArgs(accSharedMemoryPhysics));
            }
            catch (AssettoCorsaNotStartedException)
            {
            }
        }

        private void ProcessGraphics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED)
                return;

            try
            {
                _accSharedMemoryGraphics = ReadGraphics();
                OnGraphicsUpdated(new GraphicsEventArgs(_accSharedMemoryGraphics));
            }
            catch (AssettoCorsaNotStartedException)
            {
            }
        }

        private void ProcessStaticInfo()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED)
                return;

            try
            {
                ACCSharedMemoryStatic accSharedMemoryStatic = ReadStaticInfo();
                OnStaticInfoUpdated(new StaticInfoEventArgs(accSharedMemoryStatic));
            }
            catch (AssettoCorsaNotStartedException)
            {
            }
        }

        /// <summary>
        /// Read the current physics data from shared memory
        /// </summary>
        /// <returns>A Physics object representing the current status, or null if not available</returns>
        private ACCSharedMemoryPhysics ReadPhysics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED || physicsMMF == null)
                throw new AssettoCorsaNotStartedException();

            using (var stream = physicsMMF.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    var size = Marshal.SizeOf(typeof(ACCSharedMemoryPhysics));
                    var bytes = reader.ReadBytes(size);
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var data = (ACCSharedMemoryPhysics)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ACCSharedMemoryPhysics));
                    handle.Free();
                    return data;
                }
            }
        }

        private ACCSharedMemoryGraphics ReadGraphics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED || graphicsMMF == null)
                throw new AssettoCorsaNotStartedException();

            using (var stream = graphicsMMF.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    var size = Marshal.SizeOf(typeof(ACCSharedMemoryGraphics));
                    var bytes = reader.ReadBytes(size);
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var data = (ACCSharedMemoryGraphics)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ACCSharedMemoryGraphics));
                    handle.Free();
                    return data;
                }
            }
        }

        private ACCSharedMemoryStatic ReadStaticInfo()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED || staticInfoMMF == null)
                throw new AssettoCorsaNotStartedException();

            using (var stream = staticInfoMMF.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    var size = Marshal.SizeOf(typeof(ACCSharedMemoryStatic));
                    var bytes = reader.ReadBytes(size);
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var data = (ACCSharedMemoryStatic)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ACCSharedMemoryStatic));
                    handle.Free();
                    return data;
                }
            }
        }

        public string ToPrettyString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"IsRunning;{IsRunning}");
            sb.AppendLine($"IsConnected;{IsConnected}");
            sb.AppendLine($"memoryStatus;{memoryStatus}");
            sb.AppendLine($"sessionType;{sessionType}");
            sb.AppendLine($"pitStatus;{pitStatus}");
            sb.AppendLine($"gameStatus;{gameStatus}");

            return sb.ToString();
        }

        private readonly ConcurrentDictionary<int, float> _rainLevels = new();
        private const float PrecipDiffThreshold = 0.05f;
        private const float CloudCoverStartForPrecipTransition = 0.5f;
        private const float CloudCoverMinForPrecipStart = 0.6f;

        public float GetClouds(double sessionTimeElapsed, float trackToAmbientTempDelta)
        {
            if (_accSharedMemoryGraphics.Split == null)
                return 0;

            var precipNow = _accSharedMemoryGraphics.GetPrecipitation();
            var precip5MinAgo = precipNow;
            var minutesElapsed = (int)Math.Floor(sessionTimeElapsed / 60d);
            if (minutesElapsed >= 5 && _rainLevels.TryGetValue(minutesElapsed - 5, out var storedPrecip5Min))
            {
                precip5MinAgo = storedPrecip5Min;
            }

            _rainLevels[minutesElapsed] = precipNow;

            var precip10Min = ACCSharedMemoryConverters.RainIntensityEnumToFloat(_accSharedMemoryGraphics.RainIntensityIn10Min);

            var clouds = 0f;
            var isPrecip = Math.Abs(precipNow) >= 0.0001f;
            // if (Math.Abs(precipNow - precip5MinAgo) < PrecipDiffThreshold && Math.Abs(precip10Min - precipNow) < PrecipDiffThreshold)
            //     clouds = PrecipToClouds(precipNow);

            //Raining now, so trust precipNow
            if (isPrecip)
            {
                clouds = PrecipToClouds(precipNow);
                return clouds;
            }

            //When no rain right now, but we've exited a rain cell, we still have a few clouds
            if (precip5MinAgo > 0f)
            {
                clouds = PrecipTransitionToClouds(precip5MinAgo / 2f); //averaging precipNow and precip5MinAgo (but precipNow is known to be 0, so omitted from addition)
                return clouds;
            }

            //When no rain right now, but we're expecting rain in 10 minutes, clouds are building up
            if (precip5MinAgo > 0f)
            {
                clouds = PrecipTransitionToClouds(precip10Min / 2f); //averaging precipNow and precip5MinAgo (but precipNow is known to be 0, so omitted from addition)
                return clouds;
            }

            if (trackToAmbientTempDelta < 0f)
                trackToAmbientTempDelta = 0f;

            //Fully sunny - hot track compared to ambient == clear skies
            if (trackToAmbientTempDelta > 10f)
            {
                clouds = 0;
                return clouds;
            }

            //Linearly scale trackToAmbientTempDelta from range 0 - 10 to CloudCoverMinForPrecipStart - 0
            clouds = CloudCoverStartForPrecipTransition - (trackToAmbientTempDelta / 10f) * CloudCoverStartForPrecipTransition;
            return clouds;
        }

        private float PrecipToClouds(float precip)
        {
            if (precip >= 1f)
                return 1f;

            if (precip <= 0f)
                return CloudCoverMinForPrecipStart;

            return CloudCoverMinForPrecipStart + (1f - CloudCoverMinForPrecipStart) * precip;
        }

        private float PrecipTransitionToClouds(float precipTransition)
        {
            if (precipTransition >= 0.5f)
                return CloudCoverMinForPrecipStart;

            if (precipTransition <= 0f)
                return CloudCoverStartForPrecipTransition;

            return CloudCoverStartForPrecipTransition + (precipTransition / 0.5f) * (CloudCoverMinForPrecipStart - CloudCoverStartForPrecipTransition);
        }
    }
}