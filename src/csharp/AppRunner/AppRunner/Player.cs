using System;
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
        string[] inputs;
        var baseCamp = BaseCampInitializer.Init();
        IGameInput gameInput = new GameInputFactory().CreateGameInput(args);

        // heroesPerPlayer: Always 3
        int heroesPerPlayer = int.Parse(gameInput.GetInput());

        // game loop
        while (true)
        {

            inputs = gameInput.GetInput().Split(' ');
            int myHealth = int.Parse(inputs[0]); // Your base health
            int myMana = int.Parse(inputs[1]); // Ignore in the first league; Spend ten mana to cast a spell
            baseCamp.UpdateHP(myHealth).UpdateMana(myMana);

            inputs = gameInput.GetInput().Split(' ');
            int oppHealth = int.Parse(inputs[0]);
            int oppMana = int.Parse(inputs[1]);

            int entityCount = int.Parse(gameInput.GetInput()); // Amount of heros and monsters you can see
            var entityManager = new EntityManager(entityCount, gameInput);
            entityManager.Init();

            List<Hero> myHeroes = entityManager.MyHeroes;
            List<Hero> oppHeroes = entityManager.OpponentHeroes;
            List<Monster> monsters = entityManager.Monsters;

            for (int i = 0; i < heroesPerPlayer; i++)
            {
                var strategy = new DefaultActionStrategy(baseCamp, entityManager, i);
                myHeroes[i].Action(strategy);
            }

        }
    }
}


#region Input

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


public interface IActionStrategy
{
    void DoAction(Hero hero);
}

public class DefaultActionStrategy : IActionStrategy
{
    protected EntityManager entityManager;
    protected BaseCamp baseCamp;
    protected int heroIndex;
    public DefaultActionStrategy(BaseCamp baseCamp, EntityManager entityManager, int heroIndex)
    {
        this.entityManager = entityManager;
        this.baseCamp = baseCamp;
        this.heroIndex = heroIndex;
    }

    public virtual void DoAction(Hero hero)
    {
        Monster target = null;
        var monsters = entityManager.Monsters.Where(m => m.DistanceTo(baseCamp) < 5000).ToList();
        var ourThreat = monsters.Where(x => x.IsOurThreat).ToList();


        if (ourThreat.Count > 0)
        {
            target = ourThreat[heroIndex % monsters.Count];
        }

        if (target == null && monsters.Any())
        {
            target = monsters[heroIndex % monsters.Count];
        }

        if (target != null)
        {
            hero.AttackMonster(target);
        }
        else
        {
            hero.Wait();
        }
    }

}


public class Entity
{
    public int Id { get; }
    public int Type { get; }
    public Position CurrentPosition { get; }
    public int ShieldLife { get; }
    public int IsControlled { get; }
    public int Health { get; }
    public Position NextPosition { get; }
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
        this.NextPosition = new Position(vx, vy);
        this.NearBase = nearBase;
        this.ThreatFor = threatFor;
    }

    public int DistanceTo(Entity entity)
    {
        return this.CurrentPosition.CalculateDistance(entity.CurrentPosition);
    }

    public int DistanceTo(BaseCamp baseCamp)
    {
        return this.CurrentPosition.CalculateDistance(baseCamp.Location);
    }
}


public class Hero : Entity
{
    public Hero(int id, int type, int x, int y, int shieldLife, int isControlled, int health, int vx, int vy, int nearBase, int threatFor)
        : base(id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor)
    {

    }

    public void Action(IActionStrategy strategy)
    {
        strategy.DoAction(this);
    }

    public void AttackMonster(Monster monster)
    {
        Move(monster.CurrentPosition.X, monster.CurrentPosition.Y);
    }

    public void Wait()
    {
        Console.WriteLine("WAIT");
    }

    public void Move(int x, int y)
    {
        Console.WriteLine($"MOVE {x} {y}");
    }

    public void SpellWind(Position position)
    {
        Console.WriteLine($"SPELL WIND {position.X} {position.Y}");
    }

    public void Move(Position pos)
    {
        Move(pos.X, pos.Y);
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

public interface IThreatAnalyzer
{
    List<Threat> Analyze(BaseCamp baseCamp, EntityManager entityManager);
    int NumberOfHeroNeed(Threat threat);
}

public class DefaultThreatAnalyzer : IThreatAnalyzer
{
    const int NearBaseCamp = 500;
    const int HighHP = 300;
    const int CurrentThreat = 500;
    const int IntendThreat = 200;
    const int NearBaseCampDistance = 5000;
    private int maximumThreat = NearBaseCamp + HighHP + CurrentThreat + IntendThreat;

    public List<Threat> Analyze(BaseCamp baseCamp, EntityManager entityManager)
    {
        var threats = new List<Threat>();
        foreach (var monster in entityManager.Monsters)
        {
            var threat = new Threat(monster, 0);
            if (monster.DistanceTo(baseCamp) < NearBaseCampDistance)
            {
                threat.Weight += NearBaseCamp;
            }

            if (monster.IsOurThreat)
            {
                threat.Weight += CurrentThreat;
            }

            if (monster.NextPosition.X < monster.CurrentPosition.X
                && monster.NextPosition.Y < monster.CurrentPosition.Y)
            {
                threat.Weight += IntendThreat;
            }

            threats.Add(threat);
        }

        return threats.OrderByDescending(x => x.Weight).ToList();
    }

    public int NumberOfHeroNeed(Threat threat)
    {
        var weight = threat.Weight;
        if (weight == maximumThreat)
        {
            return 3;
        }
        if (weight > 1000)
        {
            return 2;
        }
        else
        {
            return 1;
        }
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

    public int NoOfHeroes { get; private set; }
    public void HandleThreat(Action action)
    {
        NoOfHeroes++;
        action.Invoke();
    }
}

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

    public void Init()
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
                            id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor
                        )
                    );
                    break;
                case TYPE_OP_HERO:
                    OpponentHeroes.Add(
                        new Hero(
                            id, type, x, y, shieldLife, isControlled, health, vx, vy, nearBase, threatFor
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

public class BaseCamp
{
    public Position Location { get; }
    public int Mana { get; private set; }
    public int HP { get; private set; }

    public BaseCamp(int x, int y)
    {
        this.Location = new Position(x, y);
    }

    public BaseCamp UpdateHP(int hp)
    {
        this.HP = hp;
        return this;
    }

    public BaseCamp UpdateMana(int mana)
    {
        this.Mana = mana;
        return this;
    }
}

public class BaseCampInitializer
{
    public static BaseCamp Init()
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');

        // base_x,base_y: The corner of the map representing your base
        int baseX = int.Parse(inputs[0]);
        int baseY = int.Parse(inputs[1]);

        return new BaseCamp(baseX, baseY);
    }
}

public class Position
{
    public int X { get; }
    public int Y { get; }
    public Position(int x, int y)
    {
        this.X = x;
        this.Y = y;
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
}