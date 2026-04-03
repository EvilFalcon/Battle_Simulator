using System;
using BattleSim.Game;
using BattleSim.Game.EventBus;
using R3;

namespace BattleSim.Mvp.GameUI
{
    public sealed class GameUIPresenter : IDisposable
    {
        private readonly IGameUIView _view;
        private readonly IBattleEventBus _eventBus;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        
        public GameUIPresenter(IGameUIView view, IBattleEventBus eventBus)
        {
            _view = view;
            _eventBus = eventBus;
        }

        public void Initialize()
        {
            //TODO This is a purely technical implementation.
            //In production, all text must pass through the localization service before being installed in the view.
            //Also, all text must be stored in SO.
            
            _view.SetStatusText("Battle!");
            _view.SetSeedText(BattleSimRuntimeState.LastUsedSeed >= 0
                ? $"Seed: {BattleSimRuntimeState.LastUsedSeed}"
                : "Seed: (random)");

            _eventBus.UnitCountChanged
                .Subscribe(c => _view.SetUnitCounts(c.team0, c.team1))
                .AddTo(_disposables);

            _eventBus.BattleEnded
                .Take(1)
                .Subscribe(winner => _view.SetStatusText($"Team {winner + 1} wins!"))
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}
