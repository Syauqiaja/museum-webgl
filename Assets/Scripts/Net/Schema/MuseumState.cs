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
	public partial class MuseumState : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public MuseumState() { }
		[Type(0, "map", typeof(MapSchema<MuseumVisitor>))]
		public MapSchema<MuseumVisitor> visitors = null;
	}
}
