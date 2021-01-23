using DemoInfo;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchTraceGenerator
{
    public sealed class PlayerMatchTraceMap : ClassMap<PlayerMatchTrace>
    {

        private List<string> HeldNames = Enum.GetValues(typeof(EquipmentElement)).Cast<EquipmentElement>().Select(v => v.ToString()).ToList();
        // TODO: Validar datos
        public PlayerMatchTraceMap()
        {

            int i = 0;
            foreach (var name in HeldNames)
            {
                Map(m => m.HeldElement, false).ConvertUsing(row =>
                {
                    return (row.HeldElement[i++ % HeldNames.Count].ToString("F5"));
                }).Name("Held" + name);
            }
            AutoMap(System.Globalization.CultureInfo.InvariantCulture);

        }
    }
}
