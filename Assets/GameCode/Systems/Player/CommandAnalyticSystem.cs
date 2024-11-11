using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Legacy.Database;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class CommandAnalyticSystem : ComponentSystem
    {
        private EntityQuery _query_requests;

        private AuthorizationSystem _auth_system;

        protected override void OnCreate()
        {
            _query_requests = GetEntityQuery(
               ComponentType.ReadOnly<CommandRequest>(),
               ComponentType.ReadOnly<CommandAnalyticTag>(),
               ComponentType.ReadOnly<NetworkMessageRaw>(),
               ComponentType.Exclude<CommandCompleteTag>()
           );

            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
        }

        protected override void OnUpdate()
        {
            var _requests = _query_requests.ToComponentDataArray<CommandRequest>(Allocator.Temp);
            var _messages = _query_requests.ToComponentDataArray<NetworkMessageRaw>(Allocator.Temp);
            var _entities = _query_requests.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < _requests.Length; ++i)
            {
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                var _index = _requests[i].index;
                var _message = _messages[i];

                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
                    var _command = (AnalyticCommandType)_message.ReadByte();
                    UnityEngine.Debug.Log($"CommandLootSystem >> Command: {_command}");
                    switch (_command)
                    {
                        #region AnalyticCommandType.TutorialStep
                        case AnalyticCommandType.TutorialStep:
                            {
                                // Save in profile analytic 'tutorial'...
                                byte step = _message.ReadByte();
                                profile.analyticEvents.AddTutorialStep(step);
                            }
                            break;
                        #endregion
                        #region AnalyticCommandType.EventOnce
                        case AnalyticCommandType.EventOnce:
                            {
                                // Save in profile analytic 'events'...
                                byte eventId = _message.ReadByte();
                                profile.analyticEvents.AddEvent(eventId);
                            }
                            break;
                        #endregion
                    }
                }
            }

            _entities.Dispose();
            _requests.Dispose();
            _messages.Dispose();
        }

    }
}