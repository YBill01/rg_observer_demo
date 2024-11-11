using Legacy.Database;
using Unity.Collections;
using Unity.Entities;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]
    public class CommandHeroSystem : ComponentSystem
    {
        private AuthorizationSystem _auth_system;
        private EntityQuery _query_upgrade;

        protected override void OnCreate()
        {
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_upgrade = GetEntityQuery(
                ComponentType.ReadOnly<CommandRequest>(),
                ComponentType.ReadOnly<CommandHeroTag>(),
                ComponentType.ReadOnly<NetworkMessageRaw>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );
        }

        protected override void OnUpdate()
        {
            var _requests = _query_upgrade.ToComponentDataArray<CommandRequest>(Allocator.TempJob);
            var _messages = _query_upgrade.ToComponentDataArray<NetworkMessageRaw>(Allocator.TempJob);
            var _entities = _query_upgrade.ToEntityArray(Allocator.TempJob);
            
            for (int i = 0; i < _requests.Length; ++i)
            {
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                var _index = _requests[i].index;
                var _message = _messages[i];
                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
                    var _command = (HeroCommandType)_message.ReadByte();
                    switch (_command)
                    {

                        /*#region HeroCommandType.Skill
                        case HeroCommandType.Skill:
                            {
                                var _hero_index = _message.ReadUShort();
                                var _skill_index = _message.ReadUShort();
                                var _hero_settings = Database.Settings.Instance.Get<HeroSettings>();

                                if (!profile.heroes.ContainsKey(_hero_index))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                var _profile_hero = profile.heroes[_hero_index];
                                if (!_profile_hero.skills.ContainsKey(_skill_index))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                if (_profile_hero.skills[_skill_index] >= _profile_hero.level)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                // TODO: skills upgrade cost ???
                                uint _skill_cost = _hero_settings.GetSkillShards(_profile_hero.skills[_skill_index]);
                                if (_skill_cost > profile.currency.shard)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                profile.currency.shard -= _skill_cost;
                                _profile_hero.skills[_skill_index]++;

                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                            }
                            break;
                        #endregion*/

                        #region HeroCommandType.Select
                        case HeroCommandType.Select:
                            {
                                /*
                                var _hero_sid = _message.ReadUShort();
                                if (!profile.heroes.ContainsKey(_hero_sid))
                                {
                                    // TODO: errors
                                    continue;
                                }
                                profile.config.hero = _hero_sid;

                                _update_selected_hero(
                                    _collection,
                                    _email,
                                    _hero_sid
                                );
                                */
                            }
                            break;
                        #endregion

                        #region HeroCommandType.Upgrade
                        case HeroCommandType.Upgrade:
                            {
                                var _hero_sid = _message.ReadUShort();

                                if (!profile.heroes.ContainsKey(_hero_sid))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                var _profile_hero = profile.heroes[_hero_sid];

                                var _need_soft = Levels.Instance.GetToUpgradeCount(_profile_hero.level, UpgradeCostType.HeroSoft);

                                if (profile.currency.soft < _need_soft)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                if (_profile_hero.level >= profile.level.level)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                // SOFT
                                var newEn = EntityManager.CreateEntity();
                                var newComponen = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = -(int)_need_soft,
                                    source_id = _hero_sid,
                                    currencyType = CurrencyType.Soft,
                                    changeSourceType = CurrencyChangeSourceType.HeroUpgrade
                                };
                                EntityManager.AddComponentData(newEn, newComponen);

                                profile.currency.soft -= _need_soft;
                                _profile_hero.level++;

                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                            }

                            break;
                        #endregion
                        
                        #region HeroCommandType.View
                        case HeroCommandType.View:
                            {
                                var _hero_sid = _message.ReadUShort();

                                if (profile.viewedHeroes.Contains(_hero_sid))
                                {
                                    continue;
                                }

                                profile.viewedHeroes.Add(_hero_sid);

                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                            }

                            break;
                        #endregion

                        #region HeroCommandType.Buy
                        case HeroCommandType.Buy:
                            {
                                ushort _hero_sid = _message.ReadUShort();

                                if (profile.heroes.ContainsKey(_hero_sid))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                if (Heroes.Instance.Get(_hero_sid, out BinaryHero binaryHero))
                                {
                                    if (binaryHero.price.isReal)
                                    {
                                        //ToDo - purchase validating
                                    }
                                    else if(binaryHero.price.isHard)
                                    {
                                        var _need_hard = binaryHero.price.hard;
                                        if (profile.currency.hard < _need_hard)
                                        {
                                            // TODO: errors
                                            continue;
                                        }

                                        // HARD
                                        var newEn = EntityManager.CreateEntity();
                                        var newComponen = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = -_need_hard,
                                            source_id = binaryHero.index,
                                            currencyType = CurrencyType.Hard,
                                            changeSourceType = CurrencyChangeSourceType.HeroBuy
                                        };
                                        EntityManager.AddComponentData(newEn, newComponen);

                                        profile.currency.hard -= _need_hard;
                                    }
                                    else if(binaryHero.price.isSoft)
                                    {
                                        var _need_soft = binaryHero.price.soft;
                                        if (profile.currency.soft < _need_soft)
                                        {
                                            // TODO: errors
                                            continue;
                                        }

                                        // SOFT
                                        var newEn = EntityManager.CreateEntity();
                                        var newComponen = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = -(int)_need_soft,
                                            source_id = binaryHero.index,
                                            currencyType = CurrencyType.Soft,
                                            changeSourceType = CurrencyChangeSourceType.HeroBuy
                                        };
                                        EntityManager.AddComponentData(newEn, newComponen);

                                        profile.currency.soft -= _need_soft;
                                    }

                                    profile.AddNewHero(_hero_sid, binaryHero);

                                    //Select new hero to deck
                                    //var _deck_index = profile.config.deck;
                                    //profile.sets[_deck_index].heroID = _hero_sid;

                                    var newEntity = EntityManager.CreateEntity();
                                    EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                                }
                                
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