using Newtonsoft.Json.Linq;

namespace DouyuBarrageDotNet
{
    public class Barrage
    {
        public string UserName { get; set; }
        public string Message { get; set; }
        public string ColorName { get; set; }
        public int Color { get; set; }

        public static Barrage FromJToken(JToken x) => new Barrage
        {
            UserName = x["nn"].Value<string>(),
            Message = x["txt"].Value<string>(),
            ColorName = (x["col"] ?? new JValue(0)).Value<int>() switch
            {
                1 => "红",
                2 => "浅蓝",
                3 => "浅绿",
                4 => "橙色",
                5 => "紫色",
                6 => "洋红",
                0 => "默认，白色",
                _ => "未知",
            },
            Color = (x["col"] ?? new JValue(0)).Value<int>() switch
            {
                1 => 0xff0000, // 红
                2 => 0x1e87f0, // 浅蓝
                3 => 0x7ac84b, // 浅绿
                4 => 0xff7f00, // 橙色
                5 => 0x9b39f4, // 紫色
                6 => 0xff69b4, // 洋红
                _ => 0xffffff, // 默认，白色
            }
        };
    }
}
