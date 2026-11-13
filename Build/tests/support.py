from pathlib import Path
import os,sys,shutil
SOURCE=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(SOURCE/'Build/runtime'));sys.path.insert(0,str(SOURCE/'Build'))
def seed_rules(root):
 root=Path(root);(root/'schema').mkdir(parents=True,exist_ok=True);shutil.copyfile(SOURCE/'Rules/generated.json',root/'schema/game_rules.json')
