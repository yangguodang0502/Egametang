﻿using System;
using System.Threading.Tasks;
using Model;

namespace Hotfix
{
	[ActorMessageHandler(AppType.Map)]
	public class Actor_TransferHandler : AMActorRpcHandler<Unit, Actor_TransferRequest, Actor_TransferResponse>
	{
		protected override async Task Run(Unit unit, Actor_TransferRequest message, Action<Actor_TransferResponse> reply)
		{
			Actor_TransferResponse response = new Actor_TransferResponse();

			try
			{
				long unitId = unit.Id;


				// 先在location锁住unit的地址
				await Game.Scene.GetComponent<LocationProxyComponent>().Lock(unitId);

				// 删除unit actorcomponent,让其它进程发送过来的消息找不到actor，重发
				unit.RemoveComponent<ActorComponent>();
				
				int mapIndex = message.MapIndex;

				StartConfigComponent startConfigComponent = Game.Scene.GetComponent<StartConfigComponent>();

				// 考虑AllServer情况
				if (startConfigComponent.Count == 1)
				{
					mapIndex = 0;
				}

				// 传送到map
				StartConfig mapConfig = startConfigComponent.MapConfigs[mapIndex];
				string address = mapConfig.GetComponent<InnerConfig>().Address;
				Session session = Game.Scene.GetComponent<NetInnerComponent>().Get(address);

				// 只删除不disponse否则M2M_TrasferUnitRequest无法序列化Unit
				Game.Scene.GetComponent<UnitComponent>().RemoveNoDispose(unitId);
				await session.Call<M2M_TrasferUnitResponse>(new M2M_TrasferUnitRequest() { Unit = unit });
				unit.Dispose();

				// 解锁unit的地址,并且更新unit的地址
				await Game.Scene.GetComponent<LocationProxyComponent>().UnLock(unitId, mapConfig.AppId);

				reply(response);
			}
			catch (Exception e)
			{
				ReplyError(response, e, reply);
			}
		}
	}
}