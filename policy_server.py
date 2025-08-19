import logging
import io
import socket
import struct
import torch
from PIL import Image
from torchvision import transforms
from lerobot.policies.factory import get_policy_class

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')


class PolicyServer:
    def __init__(self):
        self.policy_path: str = "/home/gregor/Experiments/lerobot/outputs/train/2025-08-14/11-49-17_smolvla/checkpoints/last/pretrained_model"
        self.policy_type: str = "smolvla"
        self.host: str = "127.0.0.1"
        self.port: int = 9000
        self.device = torch.device("cuda")

        # Load model
        logging.info(f"Loading policy from: {self.policy_path}")
        policy_class = get_policy_class(self.policy_type)
        self.policy = policy_class.from_pretrained(self.policy_path)
        self.policy.to(self.device)
        self.policy.eval()
        logging.info(f"Policy loaded successfully on {self.device}")

        self.image_transform = transforms.Compose([transforms.ToTensor()])

    def process_observation(self, timestamp, image_data, joint_states):
        # Prepare image
        pil_image = Image.open(io.BytesIO(image_data)).convert("RGB")
        
        image_tensor = self.image_transform(pil_image).to(self.device)

        # Prepare state
        state_tensor = torch.tensor(joint_states, dtype=torch.float32).to(self.device)

        # Create observation dict
        observation = {
            "observation.images.main": image_tensor.unsqueeze(0),
            "observation.state": state_tensor.unsqueeze(0),
            "task": "Shoot the red cup with straw",
        }
        # Get action
        with torch.no_grad():
            action_chunk = self.policy.predict_action_chunk(observation)

        # Return first action from chunk
        action = action_chunk.squeeze(0).cpu().numpy()
        return action[0] if len(action) > 0 else []

    def run(self):
        # Create socket
        server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_socket.bind((self.host, self.port))
        server_socket.listen(1)
        logging.info(f"Socket server listening on {self.host}:{self.port}")

        try:
            while True:
                # Accept connection
                client_socket, client_address = server_socket.accept()
                logging.info(f"Client connected from {client_address}")

                while True:
                    # Receive message length
                    length_data = self._receive_exact(client_socket, 4)
                    if not length_data:
                        break
                    message_length = struct.unpack('!I', length_data)[0]

                    # Receive message
                    message_data = self._receive_exact(client_socket, message_length)
                    if not message_data:
                        break

                    # Parse observation
                    timestamp, image_data, joint_states = self._parse_observation(message_data)
                    # Process and get action
                    action = self.process_observation(timestamp, image_data, joint_states)

                    # Send response
                    response = self._serialize_action(action)
                    client_socket.send(struct.pack('!I', len(response)))
                    client_socket.send(response)


        except KeyboardInterrupt:
            logging.info("Server shutting down...")
        finally:
            server_socket.close()

    def _receive_exact(self, sock, n):
        data = b''
        while len(data) < n:
            chunk = sock.recv(n - len(data))
            if not chunk:
                return None
            data += chunk
        return data

    def _parse_observation(self, data):
        data_offset = 0

        # Read timestamp
        timestamp = struct.unpack('!f', data[data_offset:data_offset+4])[0]
        data_offset += 4

        # Read image length and data
        image_length = struct.unpack('!I', data[data_offset:data_offset+4])[0]
        data_offset += 4
        image_data = data[data_offset:data_offset+image_length]
        data_offset += image_length

        # Read joint states
        joint_count = struct.unpack('!I', data[data_offset:data_offset+4])[0]
        data_offset += 4
        joint_states = []
        for _ in range(joint_count):
            value = struct.unpack('!f', data[data_offset:data_offset+4])[0]
            joint_states.append(value)
            data_offset += 4

        return timestamp, image_data, joint_states

    def _serialize_action(self, action):
        data = struct.pack('!I', len(action))
        for value in action:
            data += struct.pack('!f', float(value))
        return data


def main():
    server = PolicyServer()
    server.run()


if __name__ == "__main__":
    main()
