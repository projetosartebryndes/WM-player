# WM-player

WM-player é um player de vídeo para Windows focado em **compatibilidade de hardware**, retomada automática de reprodução e instalação simples.

## Stack escolhida

- **.NET 8 + WinForms**
  - Excelente integração nativa com Windows.
  - Aplicativo leve, estável e fácil de distribuir.
- **LibVLCSharp + VideoLAN.LibVLC.Windows**
  - Motor de reprodução com suporte amplo de codecs/containers (MP4, MKV, AVI, MOV, WMV, etc.).
  - Suporte a aceleração por hardware (D3D11VA), melhorando performance.
- **Inno Setup**
  - Gera instalador `.exe`.
  - Registra o app em “Abrir com” no Windows automaticamente.

## Funcionalidades implementadas

- Abrir arquivo de vídeo por diálogo (`Ctrl+O`) ou recebendo arquivo por argumento (duplo clique / Abrir com).
- Controles de reprodução:
  - Play/Pause (`Space`)
  - Avançar 5 segundos (`Right`)
  - Retroceder 5 segundos (`Left`)
  - Ir para início (`Home`)
  - Ir para fim (`End`)
  - Timeline para avançar/retroceder em qualquer ponto
  - Tela cheia (`F11`)
- **Retomada automática** do ponto salvo por arquivo de mídia.
- Registro automático em:
  - `Aplicativos` (Open With)
  - `SystemFileAssociations\video\shell` (menu de contexto)

## Estrutura

- `src/WMPlayer/` → código-fonte do player.
- `installer/WMPlayer.iss` → script do instalador Inno Setup.

## Build e publicação

> Requer .NET SDK 8.0 no Windows.

```bash
dotnet restore WM-player.sln
dotnet publish src/WMPlayer/WMPlayer.csproj -c Release -r win-x64 --self-contained false
```

Saída esperada:

`src/WMPlayer/bin/Release/net8.0-windows/x64/publish/`

## Gerar instalador

1. Instale o **Inno Setup 6**.
2. Abra `installer/WMPlayer.iss` no Inno Setup Compiler.
3. Compile o script.

Instalador gerado:

`installer/WM-player-setup.exe`

## Armazenamento de progresso

O progresso é salvo em:

`%LOCALAPPDATA%\WM-player\resume-state.json`

Quando o vídeo chega a ~98%+ do total, o ponto salvo é resetado para o início.
