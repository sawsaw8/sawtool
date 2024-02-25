using System.ComponentModel;

namespace sawtool;

public enum CollectStrategy
{
    [Description("Manual")]
    Manual,

    [Description("Automatic, if not overcapping")]
    NoOvercap,

    [Description("Automatic, allow overcap")]
    FullAuto,
}
