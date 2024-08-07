using AutoMapper;
using JKClient;
using JKWatcher.RandomHelpers;
using PropertyChanged;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JKWatcher
{

	class DefragAverageMapTime : INotifyPropertyChanged
	{
		[PrimaryKey]
		public string MapName { get; set; }

		[Ignore]
		public double sum { get; set; }
		[Ignore]
		public double divider { get; set; }

		object recordLock = new object();
		public Int64 record { get; set; } = Int64.MaxValue;
		public string recordHolder { get; set; } = null;
		public Int64 unloggedRecord { get; set; } = Int64.MaxValue;
		public string unloggedRecordHolder { get; set; } = null;

		public void LogTime(string player, int milliseconds, bool logged, bool isNumber1)
        {
            lock (recordLock)
			{
				if (logged)
				{
					if(isNumber1 && milliseconds < record)// milliseconds < record)
                    {
						record = milliseconds;
						recordHolder = player;
					}
				}
				else
				{
					if (milliseconds < unloggedRecord && isNumber1)
					{
						unloggedRecord = milliseconds;
						unloggedRecordHolder = player;
					}
				}
			}
			AddSample(milliseconds);
        }

		[DependsOn("sum")]
		public byte[] SumPersist
        {
            get
            {
				return BitConverter.GetBytes(sum);
            }
			set
            {
				sum = BitConverter.ToDouble(value);
            }
		}
		[DependsOn("divider")]
		public byte[] DividerPersist
        {
            get
            {
				return BitConverter.GetBytes(divider);
            }
			set
            {
				divider = BitConverter.ToDouble(value);
            }
        }

		[DependsOn("sum","divider")]
		public float ReadableAverage
        {
            get
            {
				return (float)( sum / divider);
            }
			set
            {
				// We just need to trick sqlite into saving this so we can read it nicely in db
            }
		}
		[DependsOn("divider")]
		public Int64 ReadableDivider
        {
            get
            {
				return (Int64)(Math.Round(divider)+0.5f);
            }
			set
            {
				// We just need to trick sqlite into saving this so we can read it nicely in db. We aren't restoring this value
            }
        }

		public double CurrentAverage => divider == 0 ? 0 : sum / divider;

		public event PropertyChangedEventHandler PropertyChanged;
		void AddSample(double value)
		{
			this.sum += value;
			this.divider++;
		}
		public DefragAverageMapTime()
        {
		}
		public DefragAverageMapTime(string map)
        {
			MapName = map;

		}

	}
	class IntermissionCamPosition : INotifyPropertyChanged
	{
		[PrimaryKey]
		public string MapName { get; set; }

		public float posX { get; set; }
		public float posY { get; set; }
		public float posZ { get; set; }
		public float angX { get; set; }
		public float angY { get; set; }
		public float angZ { get; set; }
		public bool trueIntermissionCam { get; set; }
		public bool trueIntermissionEntity { get; set; }
		public int nonIntermissionEntityAlgorithmVersion { get; set; }

		[Ignore]
		public Vector3 position => new Vector3(posX, posY, posZ);
		[Ignore]
		public Vector3 angles => new Vector3(angX, angY, angZ);

		public (float,float) DistanceToOther(IntermissionCamPosition other)
        {
			if (other is null) return (float.NaN, float.NaN);
			return (Vector3.Distance(this.position,other.position), Vector3.Distance(this.angles, other.angles));
        }

		public override string ToString()
        {
			return $"[{(int)posX} {(int)posY} {(int)posZ}|{(int)angX} {(int)angY} {(int)angZ}|trueIntEnt {trueIntermissionEntity}|V{nonIntermissionEntityAlgorithmVersion}]";
        }

        public event PropertyChangedEventHandler PropertyChanged;

		public LevelShotAccumType GetLevelShotAccumType()
        {
			return new LevelShotAccumType() { pos=position,angles=angles,zCompensationVersion= ProjectionMatrixHelper.ZCompensationVersion,isRealValue=true };
        }
	}



	class ServerInfoPublic
	{
		public DateTime? InfoPacketReceivedTime { get; set; } = null;
		public DateTime? StatusResponseReceivedTime { get; set; } = null;
		public bool InfoPacketReceived { get; set; } = false;
		public bool StatusResponseReceived { get; set; } = false; // If this is true, the Clients count is the actual count of clients excluding bots
		public bool NoBots { get; set; } = false;
		public string Address { get; set; }
		public string HostName { get; set; }
		public string MapName { get; set; }
		public string Game { get; set; }
		public string GameName { get; set; }
		public string GameType { get; set; }
		public int Clients { get; set; }
		public int ClientsIncludingBots { get; set; }
		public int MaxClients { get; set; }
		public int? PrivateClients { get; set; }
		public int MinPing { get; set; }
		public int MaxPing { get; set; }
		public int FPS { get; set; }
		public int Ping { get; set; }
		public bool Visibile { get; set; }
		public bool SendsAllEntities { get; set; }
		public bool NeedPassword { get; set; }
		public bool TrueJedi { get; set; }
		public bool WeaponDisable { get; set; }
		public bool ForceDisable { get; set; }
		public string Protocol { get; set; }
		public string Version { get; set; }
		public string ServerGameVersionString { get; set; }
		public string ServerSVInfoString { get; set; }
		public string Location { get; set; }
		public bool NWH { get; set; } // NWH mod detection
		public int FloodProtect { get; set; } = -1; // -1 if not yet set, -2 if server does not send it at all
		public bool Pure { get; set; }
		public string Players { get; set; }

		static IMapper mapper;
		static ServerInfoPublic() {
			MapperConfiguration configuration = new MapperConfiguration(cfg =>
			{
				//cfg.CreateMap<JKClient.ServerInfo, ServerInfoPublic>().ForMember(a=>a.Players,m=>m.MapFrom(src=>string.Join(',',src.Players)));
				cfg.CreateMap<JKClient.ServerInfo, ServerInfoPublic>().ForMember(a=>a.Players,m=>m.MapFrom(src=> JsonSerializer.Serialize(src.Players,(JsonSerializerOptions?) null)));
			});
#if DEBUG
			configuration.AssertConfigurationIsValid();
#endif
			mapper = configuration.CreateMapper();

		}

        ServerInfoPublic()
        {

        }
        public static ServerInfoPublic convertFromJKClient(ServerInfo serverInfo)
        {
			return mapper.Map<ServerInfoPublic>(serverInfo);
        }
	}
}
