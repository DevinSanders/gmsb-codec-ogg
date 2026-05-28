Test fixtures
=============

tone.ogg
  A 0.5 s, 44100 Hz, stereo OGG Vorbis file: a 440 Hz sine tone.
  Synthetically generated with ffmpeg (libvorbis), not sourced from any
  third party:

    ffmpeg -f lavfi -i "sine=frequency=440:sample_rate=44100:duration=0.5" \
           -ac 2 -c:a libvorbis -qscale:a 2 tone.ogg

  Because it is generated from a mathematical function rather than copied
  from a recording, it carries no third-party license. Public domain.
