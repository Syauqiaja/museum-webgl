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
	public partial class DakonState : BaseGameState {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public DakonState() { }
		[Type(3, "uint16")]
		public ushort centerPoolCount = default(ushort);

		[Type(4, "array", typeof(ArraySchema<string>), "string")]
		public ArraySchema<string> holes = null;

		[Type(5, "string")]
		public string activePlayer = default(string);

		[Type(6, "uint8")]
		public byte nextHoleIndex = default(byte);

		[Type(7, "array", typeof(ArraySchema<DakonSeed>))]
		public ArraySchema<DakonSeed> hand = null;

		[Type(8, "map", typeof(MapSchema<DakonStore>))]
		public MapSchema<DakonStore> storehouses = null;
	}
}
