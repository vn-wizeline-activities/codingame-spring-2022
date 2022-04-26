import math
import random
import sys

base_x, base_y = [int(i) for i in input().split()]
heroes_per_player = int(input())
is_my_base_on_top = base_x == 0

top_base = (0, 0)
bottom_base = (17630, 9000)
base_range = 5000

my_base = top_base if is_my_base_on_top else bottom_base
enemy_base = bottom_base if is_my_base_on_top else top_base

defense_point = (1900, 1600) if is_my_base_on_top else (16000, 7000)
attack_point_left = (16000, 3000) if is_my_base_on_top else (2000, 6000)
attack_point_right = (11000, 7000) if is_my_base_on_top else (6000, 1000)


def get_distance(x1, y1, x2, y2):
    return math.dist([x1, y1], [x2, y2])


def get_distance_from_my_base(_x, _y):
    return get_distance(*my_base, _x, _y)


def get_distance_from_enemy_base(_x, _y):
    return get_distance(*enemy_base, _x, _y)


class Game:
    def __init__(self):
        self.my_mana = 0

        self.spiders = []
        self.entities = []
        self.my_heroes = []
        self.enemy_heroes = []

        self.harmless_spiders = []
        self.targeting_me_spiders = []
        self.will_target_me_spiders = []
        self.will_target_enemy_spiders = []

    def _update_my_hero_coordinate(self, _id, x, y):
        for hero in self.my_heroes:
            if hero.id == _id:
                hero.set_coordinate(x, y)

    def init_game(self):
        [input() for _ in range(2)]
        entity_count = int(input())

        for i in range(entity_count):
            game_input = input().split()
            _id, _type = game_input[:2]
            if _type == "1":
                self.my_heroes.append(Hero(int(_id)))

    def refresh(self):
        self.spiders = []
        self.entities = []
        my_health, self.my_mana = [int(j) for j in input().split()]
        _, __ = [int(j) for j in input().split()]
        entity_count = int(input())

        for i in range(entity_count):
            _id, _type, x, y, shield_life, is_controlled, health, vx, vy, near_base, threat_for = [int(j) for j in
                                                                                                   input().split()]
            entity = {
                "id": _id,
                "type": _type,
                "x": x,
                "y": y,
                "shield_life": shield_life,
                "is_controlled": is_controlled,
                "health": my_health,
                "vx": vx,
                "vy": vy,
                "near_base": near_base,
                "threat_for": threat_for,
                "distance_to_my_base": get_distance_from_my_base(x, y),
                "distance_to_enemy_base": get_distance_from_enemy_base(x, y),
            }
            self.entities.append(entity)
            if _type == 0:
                self.spiders.append(entity)
            if _type == 1:
                self._update_my_hero_coordinate(_id, x, y)
            if _type == 2:
                self.enemy_heroes.append(entity)

            self.targeting_me_spiders = []
            self.will_target_me_spiders = []
            self.will_target_enemy_spiders = []
            self.harmless_spiders = []

            for spider in self.spiders:
                if spider['near_base'] == 1:
                    self.targeting_me_spiders.append(spider)
                elif spider['threat_for'] == 1:
                    self.will_target_me_spiders.append(spider)
                elif spider['threat_for'] == 2:
                    self.will_target_enemy_spiders.append(spider)
                else:
                    self.harmless_spiders.append(spider)

            self.targeting_me_spiders.sort(key=lambda s: s["distance_to_my_base"])
            self.will_target_me_spiders.sort(key=lambda s: s["distance_to_my_base"])
            self.will_target_enemy_spiders.sort(key=lambda s: s["distance_to_enemy_base"])
            self.harmless_spiders.sort(key=lambda s: s["distance_to_enemy_base"])


game = Game()


class Hero:
    def __init__(self, _id):
        self.x, self.y = None, None
        self.id = _id
        self.blow_to_enemy_base_distance = 7000
        self.wind_range = 1280
        self.control_range = 2200
        self.defence_reserve_mana = 50
        self.blow_out_of_base_distance = 1000
        print("WAIT zzz")

    def set_coordinate(self, _x, _y):
        self.x = _x
        self.y = _y

    @staticmethod
    def move_to(_x, _y):
        print(f"MOVE {_x} {_y}")

    @staticmethod
    def cast_wind(_x, _y):
        print(f"SPELL WIND {_x} {_y} !!!")

    @staticmethod
    def cast_control(_id, _x, _y):
        print(f"SPELL CONTROL {_id} {_x} {_y} ....")

    def distance_to(self, _id):
        """
        Distance from me to an entity
        """
        for entity in game.entities:
            if entity['id'] == _id:
                return get_distance(self.x, self.y, entity['x'], entity['y'])
        return 99999

    def move_to_defense_point(self):
        self.move_to(*defense_point)

    def move_attack_point_left(self):
        self.move_to(attack_point_left[0] + random.randrange(-100, 100),
                     attack_point_left[1] + random.randrange(-100, 100))

    def move_attack_point_right(self):
        self.move_to(attack_point_right[0] + random.randrange(-100, 100),
                     attack_point_right[1] + random.randrange(-100, 100))

    def defense(self):
        if game.targeting_me_spiders:
            if game.targeting_me_spiders[0]["distance_to_my_base"] < self.blow_out_of_base_distance:
                self.cast_wind(*enemy_base)
            else:
                self.move_to(game.targeting_me_spiders[0]['x'],
                             game.targeting_me_spiders[0]['y'])
        else:
            self.move_to_defense_point()

    def attack_left_of_enemy_base(self):
        for spider in game.spiders:
            if spider["distance_to_enemy_base"] < self.blow_to_enemy_base_distance:
                if get_distance(self.x, self.y, spider['x'], spider['y']) < self.wind_range \
                        and game.my_mana > self.defence_reserve_mana:
                    self.cast_wind(*enemy_base)
                    return
                self.move_to(spider['x'], spider['y'])
                return
        self.move_attack_point_left()

    def attack_right_of_enemy_base(self):
        if game.my_mana % 10 == 0:
            self.control_enemy_defence_hero()
        else:
            for spider in game.spiders:
                if spider["distance_to_enemy_base"] < self.blow_to_enemy_base_distance:
                    if get_distance(self.x, self.y, spider['x'], spider['y']) < self.wind_range \
                            and game.my_mana > self.defence_reserve_mana:
                        self.cast_wind(*enemy_base)
                        return
                    self.move_to(spider['x'], spider['y'])
                    return
            self.move_attack_point_right()

    def control_enemy_defence_hero(self):
        for hero in game.enemy_heroes:
            if self.distance_to(hero['id']) < self.control_range \
                    and hero['distance_to_enemy_base'] < base_range:
                self.cast_control(hero['id'], *my_base)
                return
        self.move_to(self.x, self.y)

game.init_game()

# game loop
while True:
    game.refresh()
    game.my_heroes[0].defense()
    game.my_heroes[1].attack_left_of_enemy_base()
    game.my_heroes[2].attack_right_of_enemy_base()
