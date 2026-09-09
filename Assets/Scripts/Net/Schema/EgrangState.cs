//
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
//
// GENERATED USING @colyseus/schema 4.0.27
//

using Colyseus.Schema;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Scripting;
#endif

namespace Museum.Net.State {
	public partial class EgrangState : BaseGameState {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public EgrangState() { }
		[Type(3, "map", typeof(MapSchema<EgrangRacer>))]
		public MapSchema<EgrangRacer> racers = null;

		[Type(4, "uint16")]
		public ushort finishUnits = default(ushort);

		[Type(5, "number")]
		public float startsAtMs = default(float);
	}
}
