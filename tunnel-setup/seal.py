"""Only encrypted personalized binaries leave the private build workspace."""
import base64,json,os,pathlib
from cryptography.hazmat.primitives import serialization,hashes
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
public=serialization.load_pem_public_key(pathlib.Path('migration-local/export-public.pem').read_bytes())
key=AESGCM.generate_key(bit_length=256);nonce=os.urandom(12)
binary=pathlib.Path('tunnel-setup/out/GAT_CONECTAR_CENTRAL.exe').read_bytes()
encrypted=AESGCM(key).encrypt(nonce,binary,b'GAT-TUNNEL-SETUP-20260901')
wrapped=public.encrypt(key,padding.OAEP(mgf=padding.MGF1(algorithm=hashes.SHA256()),algorithm=hashes.SHA256(),label=None))
envelope=json.dumps({'key':base64.b64encode(wrapped).decode(),'nonce':base64.b64encode(nonce).decode(),'ciphertext':base64.b64encode(encrypted).decode()},separators=(',',':')).encode()
text=base64.b64encode(envelope).decode()
for i in range(0,len(text),6000):print('GAT_TUNNEL_SEALED:'+text[i:i+6000])
print('PERSONALIZED_SETUP_ENCRYPTED')
