import json
from pathlib import Path
import numpy as np
from PIL import Image
from lerobot.datasets.lerobot_dataset import LeRobotDataset


# Configuration
current_script_dir = Path(__file__).parent
dataset_path = current_script_dir.parent / "Robot_shooting_dataset"
converted_dataset_path = "/home/gregor/Experiments/fanuc_shooting_sim_unity_dataset_posred"

def convert_jsonl_to_parquet():
    dataset_path_obj = Path(dataset_path)
    data_path = dataset_path_obj / "data" / "chunk-000"

    jsonl_files = [f for f in data_path.iterdir() if f.suffix == '.jsonl']
    if not jsonl_files:
        print("No JSONL files found in the data directory.")
        return

    features = {
        "action": {
            "dtype": "float32",
            "shape": (4,),
            "names": ["azimuth", "elevation_1", "elevation_2", "shoot"]
        },
        "observation.state": {
            "dtype": "float32",
            "shape": (3,),
            "names": ["azimuth_angle", "elevation_angle_1", "elevation_angle_2"]
        },
        "observation.images.main": {
            "dtype": "video",
            "shape": (480, 640, 3),
            "names": ["height", "width", "channel"],
        },
        "timestamp": {
            "dtype": "float32",
            "shape": (1,),
            "names": None
        },
    }

    dataset = LeRobotDataset.create(
        repo_id="Grigorij/fanuc_shooting_sim_unity",
        root=converted_dataset_path,
        features=features,
        fps=10,
        image_writer_processes=2,
        image_writer_threads=4,
    )

    print(f"Found {len(jsonl_files)} JSONL files to convert.")

    for jsonl_file in jsonl_files:
        # Extract episode index from filename
        episode_index = int(jsonl_file.stem.split('_')[1])
        
        # Construct path to episode frames folder
        episode_frames_folder = Path(dataset_path) / "videos" / "chunk-000" / "observation.images.main" / f"episode_{episode_index:06d}_frames"

        with open(jsonl_file, 'r') as f:
            lines = f.readlines()

        frame_index = 0

        for line in lines:
            frame_data = json.loads(line.strip())

            # Construct image path
            image_filename = f"frame_{frame_index:05d}.jpg"
            image_path = episode_frames_folder / image_filename
            
            # Load image as numpy array
            image = Image.open(image_path)
            image_array = np.array(image, dtype=np.uint8)

            # Przygotuj dane w formacie oczekiwanym przez add_frame
            lerobot_frame = {
                'action': np.array(frame_data['action'], dtype=np.float32),
                'observation.state': np.array(frame_data['observation.state'], dtype=np.float32),
                'observation.images.main': image_array
            }
            dataset.add_frame(lerobot_frame, task="Shoot the can")
            
            frame_index += 1

        dataset.save_episode()

    print(f"\nConversion complete!")

if __name__ == "__main__":
    convert_jsonl_to_parquet()