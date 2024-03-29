﻿using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 * Auto-generated code below aims at helping you parse
 * the standard input according to the problem statement.
 **/
class Player
{
    public const int TYPE_MONSTER = 0;
    public const int TYPE_MY_HERO = 1;
    public const int TYPE_OP_HERO = 2;


    static void Main(string[] args)
    {
        IGameInput gameInput = new GameInputFactory().CreateGameInput(args);
        IThreatAnalyzer threatAnalyzer = new DefaultThreatAnalyzer();

        string[] inputs;
        var myBaseCamp = BaseCampInitializer.Init(gameInput);
        BaseCamp.MyBaseCamp = myBaseCamp;
        BaseCamp.OpponentBaseCamp = myBaseCamp.Location.Equals(Position.TopLeft) ? new BaseCamp(Position.BottomRight) : new BaseCamp(Position.TopLeft);

        Logger.LogDebug($"Base Camp: {myBaseCamp.Location}");
        // heroesPerPlayer: Always 3
        int heroesPerPlayer = int.Parse(gameInput.GetInput());

        // game loop
        while (true)
        {

            inputs = gameInput.GetInput().Split(' ');
            int myHealth = int.Parse(inputs[0]); // Your base health
            int myMana = int.Parse(inputs[1]); // Ignore in the first league; Spend ten mana to cast a spell
            myBaseCamp.UpdateHP(myHealth).UpdateMana(myMana);


            inputs = gameInput.GetInput().Split(' ');
            int oppHealth = int.Parse(inputs[0]);
            int oppMana = int.Parse(inputs[1]);
            BaseCamp.OpponentBaseCamp.UpdateHP(oppHealth).UpdateMana(oppMana);

            int entityCount = int.Parse(gameInput.GetInput()); // Amount of heros and monsters you can see
            var entityManager = new EntityManager(entityCount, gameInput);
            entityManager.Init(myBaseCamp);

            List<Hero> myHeroes = entityManager.MyHeroes;
            List<Hero> oppHeroes = entityManager.OpponentHeroes;
            List<Monster> monsters = entityManager.Monsters;
            var threatAnalysisResult = threatAnalyzer.Analyze(myBaseCamp, entityManager);

            // Set roles for heroes
            myHeroes[0].IsDefensiveHero = true;
            //myHeroes[1].IsAttackOpponentBaseCamp = true;

            IActionStrategy offensiveStrategy = new OffensiveActionStrategy(threatAnalysisResult, myBaseCamp, entityManager);
            IActionStrategy defensiveStrategy = new DefensiveActionStrategy(threatAnalysisResult, myBaseCamp, entityManager);
            IActionStrategy attackOpponentStrategy = new AttackOpponentBaseCampActionStrategy(threatAnalysisResult, myBaseCamp, entityManager);
            for (int i = 0; i < heroesPerPlayer; i++)
            {
                var hero = myHeroes[i];
                IActionStrategy strategy;

                if (hero.IsDefensiveHero)
                {
                    strategy = defensiveStrategy;
                }
                else if (hero.IsAttackOpponentBaseCamp)
                {
                    strategy = attackOpponentStrategy;
                }
                else
                {
                    strategy = offensiveStrategy;
                }

                hero.Action(strategy);
            }

        }
    }
}


#region Input

public sealed class Constants
{
    public const int DefenseDistance = 5000;
    public const int DangerousDistance = 2500;
    public const int CastMana = 10;
    public const int ManaRestoredPerHit = 1;
    public const int WindEffectZone = 1280;
    public const int HighHP = 15;

    // Hero
    public const int HeroAttackZone = 800;
    public const int HeroMaxMoving = 800;
    public const int HeroAttackDmg = 2;
    public const int SpellWindRange = 1280;
    public const int SpellControlRange = 2200;
    public const int SpellShieldRange = 2200;


}

public interface IGameInput
{
    string GetInput();
}

public class ConsoleInput : IGameInput
{
    public string GetInput()
    {
        return Console.ReadLine();
    }
}

public class TextFileInput : IGameInput
{
    public TextFileInput(string textFile)
    {

    }
    public string GetInput()
    {
        // TODO: Implement read from text file. Put line into stack and pop item to input
        throw new NotImplementedException();
    }
}

public class GameInputFactory
{
    public IGameInput CreateGameInput(string[] args)
    {
        return new ConsoleInput();
    }
}

#endregion

public static class Logger
{
    public static void LogDebug(params string[] inputs)
    {
        Console.Error.WriteLine(string.Join(' ', inputs));
    }
}

#region Action strategies

public interface IActionStrategy
{
    void DoAction(Hero hero);
}

public abstract class ThreatAnalysisActionStrategy : IActionStrategy
{
    protected readonly BaseCamp baseCamp;
    protected readonly EntityManager entityManager;

    public ThreatAnalysisActionStrategy(ThreatAnalysisResult analysisResult, BaseCamp baseCamp, EntityManager entityManager)
    {
        AnalysisResult = analysisResult;
        this.baseCamp = baseCamp;
        this.entityManager = entityManager;

    }

    public ThreatAnalysisResult AnalysisResult { get; }

    public abstract void DoAction(Hero hero);
}

public class DefensiveActionStrategy : ThreatAnalysisActionStrategy
{
    public DefensiveActionStrategy(ThreatAnalysisResult analysisResult, BaseCamp baseCamp, EntityManager entityManager) : base(analysisResult, baseCamp, entityManager)
    {
    }

    public override void DoAction(Hero hero)
    {
        var target = AnalysisResult.ThreatCollection.FirstOrDefault();
        Random random = new Random();
        if (target != null && target.Monster.DistanceToMyBaseCampNextPosition() <= Constants.DefenseDistance)
        {
            Logger.LogDebug($"Defense: {target}");
            var enemies = entityManager.Monsters.Cast<Entity>().Concat(entityManager.OpponentHeroes);
            if (hero.CanSpellWind(enemies) && target.Monster.DistanceToMyBaseCamp() <= Constants.DangerousDistance)
            {
                target.HandleThreat(_ => hero.SpellWind());
            }
            else
            {
                target.HandleThreat(hero.AttackMonster);
            }

            // Enough heroes go to next target
            if (target.RequiredHeroes == target.NoOfHeroes)
            {
                AnalysisResult.ThreatCollection.Remove(target);
            }
        }
        else
        {
            if (hero.DistanceToMyBaseCamp() > Constants.DefenseDistance)
            {

                Logger.LogDebug($"Move to defense position: {baseCamp.DefensivePosition}");
                hero.Move(baseCamp.DefensivePosition);
            }
            else
            {
                hero.MoveRandomlyFromCurrent(500, "Randomly");
            }
        }
    }
}

public class OffensiveActionStrategy : ThreatAnalysisActionStrategy
{
    public OffensiveActionStrategy(ThreatAnalysisResult analysisResult, BaseCamp baseCamp, EntityManager entityManager) : base(analysisResult, baseCamp, entityManager)
    {
    }

    public override void DoAction(Hero hero)
    {
        var target = AnalysisResult.ThreatCollection.Where(x => x.RequiredHeroes > x.NoOfHeroes).FirstOrDefault();
        Random random = new Random();
        if (target != null)
        {
            Logger.LogDebug($"Offensive: {target}");
            var enemies = entityManager.Monsters.Cast<Entity>().Concat(entityManager.OpponentHeroes);
            if (hero.CanSpellWind(enemies) && (baseCamp.Mana.Value > 50 || target.RequiredHeroes > 1))
            {
                hero.SpellWind();
                target.HandleThreat(_ => { });
            }
            else if (hero.CanSpellControl(entityManager.Monsters, out Entity enemy) 
                && (baseCamp.Mana.Value > 19 || target.RequiredHeroes > 1)
                && enemy.DistanceToOpponentBaseCamp() > hero.DistanceToOpponentBaseCamp()
                && enemy.IsControlled == 0)
            {
                hero.SpellControl(enemy, BaseCamp.OpponentBaseCamp.Location);
                target.HandleThreat(_ => { });
            }
            else
            {
                target.HandleThreat(hero.AttackMonster);
            }

            // Enough heroes go to next target
            if (target.RequiredHeroes == target.NoOfHeroes)
            {
                AnalysisResult.ThreatCollection.Remove(target);
            }
        }
        else
        {
            var newPosition = new Position(hero.CurrentPosition.X + baseCamp.OffensiveDirection(random.Next(800)), hero.CurrentPosition.Y + baseCamp.OffensiveDirection(random.Next(800)));

            if (hero.DistanceToMyBaseCamp() < 10000)
            {
                hero.Move(newPosition);
            }
            else
            {
                hero.Wait();
            }

        }
    }
}

public class AttackOpponentBaseCampActionStrategy : ThreatAnalysisActionStrategy
{
    public AttackOpponentBaseCampActionStrategy(ThreatAnalysisResult analysisResult,
                                                BaseCamp baseCamp,
                                                EntityManager entityManager) : base(analysisResult, baseCamp, entityManager)
    {
    }

    public override void DoAction(Hero hero)
    {

        Random random = new Random();
        Position holdPosition = new Position(BaseCamp.OpponentBaseCamp.Location.X - baseCamp.OffensiveDirection(3000), BaseCamp.OpponentBaseCamp.Location.Y - baseCamp.OffensiveDirection(3000));

        var reachToOpponentDistance = holdPosition.CalculateDistance(BaseCamp.OpponentBaseCamp.Location);
        var distanceToBaseCamp = hero.DistanceToOpponentBaseCamp();
        if (distanceToBaseCamp > reachToOpponentDistance || distanceToBaseCamp < Constants.DangerousDistance)
        {
            hero.Move(holdPosition, "Move to position");
        }
        else
        {
            var enemies = entityManager.Monsters.Cast<Entity>().Concat(entityManager.OpponentHeroes);
            if (hero.CanSpellWind(enemies) && baseCamp.Mana.Value > 11)
            {
                hero.SpellWind();
            }
            else
            {
                
                hero.MoveRandomlyFromCurrent(800, "Randomly");
            }
        }
    }
}

#endregion

#region Entities


public class EntityManager
{
    public const int TYPE_MONSTER = 0;
    public const int TYPE_MY_HERO = 1;
    public const int TYPE_OP_HERO = 2;
    public List<Hero> MyHeroes = new List<Hero>();
    public List<Hero> OpponentHeroes = new List<Hero>();
    public List<Monster> Monsters = new List<Monster>();

    public int EntityCount { get; }
    public IGameInput GameInput { get; }

    public EntityManager(int count, IGameInput gameInput)
    {
        EntityCount = count;
        GameInput = gameInput;
    }

    public void Init(BaseCamp baseCamp)
    {
        for (int i = 0; i < EntityCount; i++)
        {
            var inputs = GameInput.GetInput().Split(' ');
            int id = int.Parse(inputs[0]); // Unique identifier
            int type = int.Parse(inputs[1]); // 0=monster, 1=your hero, 2=opponent hero
            int x = int.Parse(inputs[2]); // Position of this entity
            int y = int.Parse(inputs[3]);
            int shieldLife = int.Parse(inputs[4]); // Ignore for this league; Count down until shield spell fades
            int isControlled = int.Parse(inputs[5]); // Ignore for this league; Equals 1 when this entity is under a control spell
            int health = int.Parse(inputs[6]); // Remaining health of this monster
            int vx = int.Parse(inputs[7]); // Trajectory of this monster
            int vy = int.Parse(inputs[8]);
            int nearBase = int.Parse(inputs[9]); // 0=monster with no target yet, 1=monster targeting a base
            int threatFor = int.Parse(inputs[10]); // Given this monster's trajectory, is it a threat to 1=your base, 2=your opponent's base, 0=neither

            switch (type)
            {
                case TYPE_MONSTER:
                    Monsters.Add(
                        new Monster(
                            id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor
                        )
                    );

                    break;
                case TYPE_MY_HERO:
                    MyHeroes.Add(
                        new Hero(
                            id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor, baseCamp
                        )
                    );
                    break;
                case TYPE_OP_HERO:
                    OpponentHeroes.Add(
                        new Hero(
                            id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor, null
                        )
                    );
                    break;
                default:
                    throw new ArgumentException("Type is undefined");
            }
        }

    }

    public static bool IsOpponentHero(int type) => type == TYPE_OP_HERO;
    public static bool IsMonster(int type) => type == TYPE_MONSTER;
    public static bool IsMyHero(int type) => type == TYPE_MY_HERO;
}

public class Entity
{
    public int Id { get; }
    public int Type { get; }
    public Position CurrentPosition { get; }
    public int ShieldLife { get; }
    public int IsControlled { get; set; }
    public int Health { get; }
    public Velocity SpeedVector { get; }
    public int NearBase { get; }
    public int ThreatFor { get; }



    public Entity(int id, int type, int x, int y, int shieldLife, int isControlled, int health, int vx, int vy, int nearBase, int threatFor)
    {
        this.Id = id;
        this.Type = type;
        this.CurrentPosition = new Position(x, y);
        this.ShieldLife = shieldLife;
        this.IsControlled = isControlled;
        this.Health = health;
        this.SpeedVector = new Velocity(vx, vy);
        this.NearBase = nearBase;
        this.ThreatFor = threatFor;
    }

    public int DistanceTo(Entity entity, bool nextTurn = false)
    {
        return this.CurrentPosition.CalculateDistance(nextTurn ? entity.NextPosition() : entity.CurrentPosition);
    }


    public Position NextPosition()
    {
        return new Position(this.CurrentPosition.X + SpeedVector.X, this.CurrentPosition.Y + SpeedVector.Y);
    }

    public override string ToString()
    {
        return $"Type: {this.GetType().Name}, Id: {Id}, NearBase: {NearBase}, Threat: {ThreatFor}, Position: {CurrentPosition}, Speed: {SpeedVector}, Health: {Health}, Shield: {ShieldLife}, Control: {IsControlled}";
    }
}


public class Hero : Entity
{
    const int CastMana = 10;
    private readonly Mana mana;

    public BaseCamp BaseCamp { get; }

    public Hero(int id, int type, int x, int y, int shieldLife, int isControlled, int health, int vx, int vy, int nearBase, int threatFor, BaseCamp baseCamp)
        : base(id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor)
    {
        BaseCamp = baseCamp;
        this.mana = baseCamp?.Mana;
    }

    public void Action(IActionStrategy strategy)
    {
        strategy.DoAction(this);
    }

    public bool IsDefensiveHero { get; set; }

    public bool IsAttackOpponentBaseCamp { get; set; }

    public void AttackMonster(Monster monster)
    {
        mana.IncreaseMana();
        Move(monster.CurrentPosition.X, monster.CurrentPosition.Y, "Attack Monster");
    }

    public void Wait()
    {
        Console.WriteLine("WAIT");
    }

    public void Move(int x, int y, string comment = "")
    {
        CurrentPosition.UpdatePosition(x, y);
        Console.WriteLine($"MOVE {x} {y} #{comment ?? "<comment>"}");
    }

    public void MoveFromCurrentPosition(int x, int y, string comment = null)
    {
        var newX = CurrentPosition.X + x;
        var newY = CurrentPosition.Y + y;
        CurrentPosition.UpdatePosition(newX, newY);
        Console.WriteLine($"MOVE {newX} {newY} #{comment ?? "n/a"}");
    }


    public void MoveRandomlyFromCurrent(int maxDistance ,string comment = "")
    {
        var random = new Random();

        var x = random.Next(maxDistance) - random.Next(maxDistance);
        var y = random.Next(maxDistance) - random.Next(maxDistance);
        MoveFromCurrentPosition(x, y, comment);

    }

    public void SpellWind()
    {
        mana.DecreaseMana(CastMana);
        int x = CurrentPosition.X + BaseCamp.OffensiveDirection(SpeedVector.X) > 0 ? CurrentPosition.X + BaseCamp.OffensiveDirection(1000) : 0;
        int y = CurrentPosition.Y + BaseCamp.OffensiveDirection(SpeedVector.X) > 0 ? CurrentPosition.Y + BaseCamp.OffensiveDirection(1000) : 0;
        string spellCommand = $"SPELL WIND {x} {y}";
        Console.WriteLine(spellCommand);
    }

    public void SpellControl(Entity controlledEntity, Position newPosition)
    {
        controlledEntity.IsControlled = 1;
        string spellCommand = $"SPELL CONTROL {controlledEntity.Id} {newPosition.X} {newPosition.Y}";
        Logger.LogDebug(spellCommand);
        Console.WriteLine(spellCommand);
    }

    public void SpellShield(Entity protectedEntity)
    {
        string spellCommand = $"SPELL SHIELD {protectedEntity.Id}";
        Logger.LogDebug(spellCommand);
        Console.WriteLine(spellCommand);
    }

    public void Move(Position pos, string comment = null)
    {
        Move(pos.X, pos.Y, comment);
    }

    public bool CanSpellWind(IEnumerable<Entity> enemies)
    {
        int nearbyMonsters = FindNearByEnemies(enemies, Constants.SpellWindRange).Count();

        Logger.LogDebug($"Current Position: {CurrentPosition} - Next: {SpeedVector}");
        if (mana.Value >= CastMana && nearbyMonsters > 0)
        {
            return true;
        }

        return false;
    }

    private IEnumerable<Entity> FindNearByEnemies(IEnumerable<Entity> enemies, int effectRange)
    {
        return enemies.Where(x => x.CurrentPosition.CalculateDistance(this.CurrentPosition) <= effectRange);
    }

    public bool CanSpellControl(IEnumerable<Entity> enemies, out Entity enemy)
    {
        enemy = FindNearByEnemies(enemies, Constants.SpellWindRange).FirstOrDefault();

        if (mana.Value >= CastMana && enemy != null)
        {
            return true;
        }

        return false;
    }

}



public class Monster : Entity
{
    public Monster(int id, int type, int x, int y, int shieldLife, int isControlled, int health, int vx, int vy, int nearBase, int threatFor)
        : base(id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor)
    {

    }

    public bool IsOurThreat => this.ThreatFor == 1;
}

#endregion

#region Threat Analysis

public interface IThreatAnalyzer
{
    ThreatAnalysisResult Analyze(BaseCamp baseCamp, EntityManager entityManager);
    int CalculateRequiredHeroes(Threat threat);
}

public class ThreatAnalysisResult
{
    public ThreatAnalysisResult(int numberOfMonsters)
    {
        this.ThreatCollection = new ThreatCollection(numberOfMonsters);
    }

    public ThreatCollection ThreatCollection { get; }
}

public class ThreatCollection : List<Threat>
{
    public ThreatCollection(int capacity) : base(capacity)
    {
    }
}

public class DefaultThreatAnalyzer : IThreatAnalyzer
{
    const int W_NearBaseCamp = 500;
    const int W_DangerousZone = 700;
    const int W_HighHP = 300;
    const int W_CurrentThreat = 500;
    const int W_IntendThreat = 200;
    private int maximumThreat = W_NearBaseCamp + W_HighHP + W_CurrentThreat + W_IntendThreat + W_DangerousZone;

    public ThreatAnalysisResult Analyze(BaseCamp baseCamp, EntityManager entityManager)
    {
        var analysisResult = new ThreatAnalysisResult(entityManager.Monsters.Count);
        var threats = new List<Threat>();
        foreach (var monster in entityManager.Monsters)
        {
            var threat = new Threat(monster, 0);
            if (monster.DistanceToMyBaseCamp() < Constants.DefenseDistance)
            {
                threat.Weight += W_NearBaseCamp;
            }

            if (monster.DistanceToMyBaseCamp() < Constants.DangerousDistance)
            {
                threat.Weight += W_DangerousZone;
                threat.Weight += Constants.DangerousDistance - monster.DistanceToMyBaseCamp();
            }


            if (monster.Health > Constants.HighHP)
            {
                threat.Weight += W_HighHP;
            }

            if (monster.IsOurThreat)
            {
                threat.Weight += W_CurrentThreat;
            }

            if (monster.DistanceToMyBaseCamp() < monster.DistanceToMyBaseCampNextPosition())
            {
                threat.Weight += W_IntendThreat;
            }

            threat.RequiredHeroes = CalculateRequiredHeroes(threat);
            threats.Add(threat);
        }

        analysisResult.ThreatCollection.AddRange(threats.OrderByDescending(x => x.Weight).ToList());

        return analysisResult;
    }

    public int CalculateRequiredHeroes(Threat threat)
    {
        var weight = threat.Weight;
        if (weight >= W_DangerousZone + W_CurrentThreat + W_NearBaseCamp)
        {
            return 3;
        }
        if (weight >= W_CurrentThreat + W_NearBaseCamp)
        {
            return 1;
        }
        else if (weight > 0)
        {
            return 1;
        }

        return 0;
    }
}

public class Threat
{
    public Monster Monster { get; }
    public int Weight { get; set; }
    public Threat(Monster monster, int weight)
    {
        Monster = monster;
        Weight = weight;
    }
    public int RequiredHeroes { get; set; }
    public int NoOfHeroes { get; private set; }
    public void HandleThreat(Action<Monster> action)
    {
        NoOfHeroes++;
        action.Invoke(this.Monster);
    }

    public override string ToString()
    {
        return $"Threat level: {Weight}, Required: {RequiredHeroes}, Handled: {NoOfHeroes}, Target: {Monster}";
    }
}
#endregion

#region Base camp

public class BaseCamp
{
    private const int DefensiveDistance = 1500;

    public Position Location { get; }
    public Mana Mana { get; private set; }
    public int HP { get; private set; }
    public BaseCamp(Position location) : this(location.X, location.Y)
    {

    }
    public BaseCamp(int x, int y)
    {
        this.Location = new Position(x, y);
        this.Mana = new Mana(0);

        if (Location.Equals(Position.TopLeft))
        {
            DefensivePosition = new Position(DefensiveDistance, DefensiveDistance);
        }
        else
        {
            DefensivePosition = new Position(Location.X - DefensiveDistance, Location.Y - DefensiveDistance);
        }
    }

    public BaseCamp UpdateHP(int hp)
    {
        this.HP = hp;
        return this;
    }

    public BaseCamp UpdateMana(int mana)
    {
        this.Mana.Value = mana;
        return this;
    }

    public int OffensiveDirection(int value)
    {
        int newValue = value;
        if (Location.Equals(Position.BottomRight))
        {
            newValue = value * (-1);
        }

        Logger.LogDebug($"New value: {newValue}");
        return newValue;
    }

    public Position DefensivePosition { get; }

    /// <summary>
    /// My base camp
    /// </summary>
    /// <remarks> The value will be set at init game </remarks>
    public static BaseCamp MyBaseCamp { get; set; }

    /// <summary>
    /// My opponent camp
    /// </summary>
    /// <remarks> The value will be set at init game </remarks>
    public static BaseCamp OpponentBaseCamp { get; set; }
}

public class Mana
{
    public Mana(int value)
    {
        Value = value;
    }

    public int Value { get; set; }
    public void IncreaseMana()
    {
        Value++;
    }

    public void DecreaseMana(int value)
    {
        Value -= value;
    }
}

public sealed class BaseCampInitializer
{
    public static BaseCamp Init(IGameInput gameInput)
    {
        string[] inputs;
        inputs = gameInput.GetInput().Split(' ');
        // base_x,base_y: The corner of the map representing your base
        int baseX = int.Parse(inputs[0]);
        int baseY = int.Parse(inputs[1]);

        return new BaseCamp(baseX, baseY);
    }
}
#endregion

#region Others


public class Velocity
{
    public Velocity(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }

    public override string ToString()
    {
        return $"X={X}, Y={Y}";
    }
}

public class Position : IEquatable<Position>
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public Position(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }

    public void UpdatePosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Position Move(int x, int y)
    {
        return new Position(X + x, Y + y);
    }

    public int CalculateDistance(Position toPosition)
    {
        return CalculateDistance(toPosition.X, toPosition.Y);
    }

    public int CalculateDistance(int toX, int toY)
    {
        int deltaX = Math.Abs(toX - X);
        int deltaY = Math.Abs(toY - Y);

        return (int)Math.Floor(Math.Sqrt(deltaX * deltaX + deltaY * deltaY));
    }

    public override string ToString()
    {
        return $"X={X}, Y={Y}";
    }

    public bool Equals(Position? other)
    {
        if (other == null)
            return false;

        return X == other.X && Y == other.Y;
    }

    public override int GetHashCode()
    {
        return $"{X}, {Y}".GetHashCode();
    }


    public static Position TopLeft => new Position(0, 0);
    public static Position BottomRight => new Position(17630, 9000);


}
#endregion


#region Extensions

public static class EntityExtenstions
{
    public static int DistanceToMyBaseCamp(this Entity entity)
    {
        return entity.CurrentPosition.CalculateDistance(BaseCamp.MyBaseCamp.Location);
    }

    public static int DistanceToMyBaseCampNextPosition(this Entity entity)
    {
        return entity.NextPosition().CalculateDistance(BaseCamp.MyBaseCamp.Location);
    }

    public static int DistanceToOpponentBaseCamp(this Entity entity)
    {
        return entity.CurrentPosition.CalculateDistance(BaseCamp.OpponentBaseCamp.Location);
    }
}

#endregion