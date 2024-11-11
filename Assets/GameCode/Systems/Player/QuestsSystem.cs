using Legacy.Database;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;
using Unity.Mathematics;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class QuestsSystem : JobComponentSystem
    {
        private EndInitializationEntityCommandBufferSystem _barrier;


        private EntityQuery _query_quests;
        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
        {
            _barrier = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            _query_quests = GetEntityQuery(
                ComponentType.ReadOnly<QuestCompleteData>()
            );
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
            _player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();

            RequireForUpdate(_query_quests);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var _command_buffer = _barrier.CreateCommandBuffer();
            inputDeps = new QuestCompleteJob
            {
                buffer = _command_buffer,
                auth = _auth_system
            }.Schedule(_query_quests, inputDeps);
            

            return inputDeps;
        }

        public struct QuestCompleteJob : IJobForEachWithEntity<QuestCompleteData>
        {
            [ReadOnly] public AuthorizationSystem auth;
            public EntityCommandBuffer buffer;

            public void Execute(Entity entity, int index, ref QuestCompleteData quest)
            {
                if (auth.Profiles.TryGetValue(quest.user_index, out PlayerProfileInstance profile))
                {
                    profile.daylics.TryComplete(quest);
                }
                buffer.DestroyEntity(entity);
            }
        }

    }
}

