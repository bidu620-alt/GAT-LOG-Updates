#!/usr/bin/env python3
"""Produce a real BiduLog walkthrough video from the public site screenshots."""
from pathlib import Path
import math
import subprocess
import shutil
from PIL import Image, ImageOps, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "video"
OUT.mkdir(parents=True, exist_ok=True)
W, H, FPS = 1280, 720, 24

SCENES = [
 ("01-pagina-inicial.jpg", "BEM-VINDO\nAO BIDULOG",
  "Sua carreira nas estradas\ncomeça aqui.",
  "Bem-vindo ao BiduLog. Uma plataforma que transforma suas viagens no Euro Truck Simulator 2 em conquistas, desafios e uma carreira virtual."),
 ("02-area-motorista.jpg", "PERFIL DO\nMOTORISTA",
  "Viagens, histórico,\nexperiência e progressão.",
  "Cada motorista possui um espaço para acompanhar sua conta, suas entregas, sua experiência e a evolução nas estradas."),
 ("03-ranking.jpg", "RANKING E\nCOMPETIÇÃO",
  "Compare resultados e\nconquiste posições.",
  "No ranking, você acompanha os melhores resultados da comunidade, disputa posições e valoriza cada ponto conquistado."),
 ("04-passe-mensal.jpg", "PASSE E\nDESAFIOS",
  "Metas especiais e\nnovos objetivos.",
  "O Passe do BiduLog reúne desafios de diferentes categorias e metas que dão novos motivos para continuar dirigindo."),
 ("05-mapa-ao-vivo.jpg", "MAPA AO\nVIVO",
  "Acompanhe a\ncomunidade na estrada.",
  "Com o mapa ao vivo, você acompanha as informações disponíveis sobre motoristas conectados e viagens pela comunidade."),
 ("06-loja-economia.jpg", "GARAGEM E\nECONOMIA",
  "Marcas, Gold e\nrecompensas.",
  "A economia BiduLog inclui garagem, marcas de veículos e Gold, sempre conforme as regras de validação das entregas."),
 ("07-eventos.jpg", "EVENTOS\nESPECIAIS",
  "Cada evento, um\nnovo desafio.",
  "Participe dos eventos, complete as entregas de cada desafio e registre novas conquistas em sua carreira."),
 ("08-motoristas-em-live.jpg", "MOTORISTAS\nEM LIVE",
  "Assista à comunidade\nna Twitch e no YouTube.",
  "Acompanhe também os criadores de conteúdo da comunidade nas transmissões disponíveis no site."),
]
INTRO = "BiduLog. A cada viagem, uma história."
OUTRO = "Conheça o BiduLog. Acesse bidulog ponto com ponto bê erre e comece sua jornada."

def font(size, bold=False):
    candidates = [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    ]
    for path in candidates:
        if Path(path).exists(): return ImageFont.truetype(path, size)
    return ImageFont.load_default()

def canvas():
    im = Image.new("RGB", (W, H))
    pix = im.load()
    for y in range(H):
        for x in range(W):
            n = min(1, (x+y*.32)/(W*1.2))
            pix[x,y] = (int(5+8*n), int(20+20*n), int(35+23*n))
    return im

def text(draw, xy, label, size, color, bold=False, spacing=12):
    draw.multiline_text(xy, label, font=font(size,bold), fill=color, spacing=spacing)

def composed(path, title, subtitle, number):
    image = Image.open(ROOT/path).convert("RGB")
    bg = canvas()
    d = ImageDraw.Draw(bg)
    d.rectangle((0,0,9,H),fill=(0,192,229))
    d.rounded_rectangle((18,20,300,66),radius=9,fill=(14,72,96))
    text(d,(34,30),"BIDULOG  /  ETS2",22,(224,247,251),True)
    text(d,(27,150),title,43,(245,249,252),True,spacing=8)
    d.rectangle((29,355,90,360),fill=(0,194,223))
    text(d,(29,382),subtitle,24,(180,211,223),False,spacing=13)
    text(d,(30,643),"bidulog.com.br",23,(0,212,239),True)
    text(d,(260,649),"%02d / 08" % number,13,(155,192,207),True)
    # Entire original screenshot, avoiding the creation of fictional page elements.
    shot=ImageOps.contain(image,(900,570),Image.Resampling.LANCZOS)
    pane=Image.new("RGB",(916,586),(17,44,59))
    mask=Image.new("L",pane.size,0)
    ImageDraw.Draw(mask).rounded_rectangle((0,0,pane.width-1,pane.height-1),radius=16,fill=255)
    bg.paste(pane,(350,69),mask)
    bg.paste(shot,(358+(900-shot.width)//2,77+(570-shot.height)//2))
    d=ImageDraw.Draw(bg)
    d.rounded_rectangle((349,68,1266,656),radius=16,outline=(30,174,192),width=2)
    return bg

def titleframe(end=False):
    bg=canvas()
    d=ImageDraw.Draw(bg)
    for i in range(8):
        d.line((0,520+i*21,W,260+i*38),fill=(10,56+i*5,73+i*4),width=3)
    d.rounded_rectangle((420,105,862,154),radius=10,fill=(8,102,130))
    text(d,(445,115),"EURO TRUCK SIMULATOR 2",23,(235,248,253),True)
    text(d,(253,235),"BIDULOG",105,(245,251,255),True)
    d.line((255,393,1028,393),fill=(0,206,225),width=5)
    text(d,(365,425),"A cada viagem, uma história.",30,(191,228,238))
    if end:
        text(d,(453,555),"BIDULOG.COM.BR",32,(21,210,234),True)
    else:
        text(d,(423,555),"SUA CARREIRA NAS ESTRADAS",24,(21,210,234),True)
    return bg

def voice(text_, idx):
    outfile=OUT/("voz_%02d.mp3"%idx)
    try:
        from gtts import gTTS
        gTTS(text=text_, lang="pt", tld="com.br", slow=False, timeout=12).save(str(outfile))
        if outfile.stat().st_size<1024: return None
        return outfile
    except Exception as e:
        print("Narration unavailable for scene",idx,":",e,flush=True)
        return None

def audio_duration(path):
    if not path:return 0
    result=subprocess.run(["ffprobe","-v","error","-show_entries","format=duration","-of","default=nokey=1:noprint_wrappers=1",str(path)],capture_output=True,text=True)
    try:return float(result.stdout.strip())
    except:return 0

def clip(image, idx, spoken, baseline):
    imagepath=OUT/("cena_%02d.jpg"%idx)
    image.save(imagepath,quality=92)
    spokenfile=voice(spoken,idx)
    length=math.ceil(max(baseline,audio_duration(spokenfile)+1.4))
    dest=OUT/("parte_%02d.mp4"%idx)
    filters = (
      "zoompan=z='min(zoom+0.000045,1.06)':"
      "x='iw/2-iw/zoom/2':y='ih/2-ih/zoom/2':d=1:"
      "s=1280x720:fps=24,"
      "fade=t=in:st=0:d=0.35,"
      "fade=t=out:st=%s:d=0.45" % (length-.45)
    )
    command=["ffmpeg","-hide_banner","-loglevel","error","-y",
      "-loop","1","-framerate",str(FPS),"-i",str(imagepath)]
    if spokenfile:
        command+=["-i",str(spokenfile)]
    else:
        command+=["-f","lavfi","-i","anullsrc=channel_layout=stereo:sample_rate=48000"]
    command+=["-vf",filters,"-af","apad",
      "-t",str(length),"-c:v","libx264","-preset","veryfast","-crf","24",
      "-pix_fmt","yuv420p","-r",str(FPS),"-c:a","aac","-b:a","128k",
      "-ar","48000","-ac","2","-movflags","+faststart",str(dest)]
    subprocess.run(command,check=True)
    print("Rendered",dest.name,length,"seconds, narration",bool(spokenfile),flush=True)
    return dest

def main():
    required=[ROOT/name for name,*_ in SCENES]
    missing=[str(p) for p in required if not p.exists()]
    if missing: raise RuntimeError("Screenshots missing: "+", ".join(missing))
    parts=[clip(titleframe(),0,INTRO,5)]
    for n,(path,title,sub,spoken) in enumerate(SCENES,1):
        parts.append(clip(composed(path,title,sub,n),n,spoken,8))
    parts.append(clip(titleframe(True),9,OUTRO,6))
    manifest=OUT/"lista.txt"
    manifest.write_text("".join("file '%s'\n"%str(p.resolve()) for p in parts),encoding="utf8")
    final=ROOT/"BiduLog_Apresentacao.mp4"
    subprocess.run(["ffmpeg","-hide_banner","-loglevel","error","-y",
      "-f","concat","-safe","0","-i",str(manifest),
      "-c","copy","-movflags","+faststart",str(final)],check=True)
    print("FINAL:",final,"BYTES:",final.stat().st_size,flush=True)

if __name__=="__main__":main()
