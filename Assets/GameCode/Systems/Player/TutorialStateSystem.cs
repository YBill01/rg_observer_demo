using Unity.Entities;
using Unity.Collections;
using Legacy.Database;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class TutorialStateSystem : ComponentSystem
    {
        EntityQuery _query_results;
        private AuthorizationSystem _auth_system;

        protected override void OnCreate()
        {
            _query_results = GetEntityQuery(
                ComponentType.ReadOnly<ObserverTutorialUpdateState>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );

            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
        }

        protected override void OnUpdate()
        {
            var _requests = _query_results.ToComponentDataArray<ObserverTutorialUpdateState>(Allocator.TempJob);
            var _entities = _query_results.ToEntityArray(Allocator.TempJob);

            for (int i = 0; i < _requests.Length; ++i)
            {
                var _request = _requests[i];

                if (_auth_system.Profiles.TryGetValue(_request.player, out PlayerProfileInstance profile))
                {
                    profile.tutorial.hard_tutorial_state = _request.hard_tutorial_state;
                    profile.tutorial.menu_tutorial_state = _request.menu_tutorial_state;
                    profile.session.time = System.DateTime.UtcNow;
                    if (!profile.tutorial.tutorials_steps.ContainsKey(_request.senario_index))
                    {
                        profile.tutorial.tutorials_steps.Add(_request.senario_index, _request.menu_tutorial_step);
                    }
                    else
                    {
                        profile.tutorial.tutorials_steps[_request.senario_index] = _request.menu_tutorial_step;
                    }

                    //var newEntity = EntityManager.CreateEntity();
                    //EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _request.player });
                }
                
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());
            }
            
            _entities.Dispose();
            _requests.Dispose();
            //EntityManager.DestroyEntity(_query_results);
        }
    }
}