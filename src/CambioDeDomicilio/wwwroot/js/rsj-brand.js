(function(){
    const STATE = {
      theme: 'dark',
      helixStyle: 'scrolltide',
      helixSpeed: 0.85,
      assemblyMode: 'loop',
      scaleWidth: 100,
      gapPx: 4,
      fontStyle: 'arial',
      blueprintTicks: false,
      hoverInteraction: true
    };

    const HELIX_DUST = (() => {
      let s = 4099;
      const rnd = () => {
        s = (s * 1664525 + 1013904223) >>> 0;
        return s / 4294967296;
      };
      const arr = [];
      for (let i = 0; i < 36; i++) {
        arr.push({
          v: rnd(),
          angle: rnd() * Math.PI * 2,
          dist: 0.52 + rnd() * 1.18,
          size: 0.85 + rnd() * 2.1,
          speed: 0.35 + rnd() * 0.95,
          gold: rnd() < 0.34
        });
      }
      return arr;
    })();

    /**
     * =========================================================================
     * 2. MOTOR DE RENDERIZADO 3D HIPERREALISTA (PBR + SUBSURFACE + CINEMÁTICA < >)
     *    - Corchetes < > con rotación 3D, bisel metálico especular, respiración y pulso
     *    - Nodos atómicos con iluminación esférica 3D, oclusión y reflejo de lente
     *    - Hebras cilíndricas sombreadas en volumen + puentes de hidrógeno con fotones
     * =========================================================================
     */
    function drawMetallicBracket3D(ctx, pts, thickness, isDark, timeSec, sideSign, unit, hyperMode) {
      const [pTop, pMid, pBot] = pts;

      // 1. Sombra proyectada profunda (oclusión espacial bajo el corchete)
      ctx.save();
      ctx.strokeStyle = isDark ? 'rgba(2, 6, 16, 0.72)' : 'rgba(15, 23, 42, 0.22)';
      ctx.lineWidth = thickness * 1.18;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.shadowColor = isDark ? 'rgba(0, 0, 0, 0.85)' : 'rgba(30, 58, 95, 0.35)';
      ctx.shadowBlur = 18 * unit;
      ctx.shadowOffsetY = 8 * unit;
      ctx.beginPath();
      ctx.moveTo(pTop.x, pTop.y);
      ctx.lineTo(pMid.x, pMid.y);
      ctx.lineTo(pBot.x, pBot.y);
      ctx.stroke();
      ctx.restore();

      // 2. Halo energético exterior (bloom sutil)
      if (hyperMode) {
        ctx.save();
        ctx.strokeStyle = isDark ? 'rgba(120, 165, 220, 0.18)' : 'rgba(88, 120, 155, 0.12)';
        ctx.lineWidth = thickness * 1.45;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.beginPath();
        ctx.moveTo(pTop.x, pTop.y);
        ctx.lineTo(pMid.x, pMid.y);
        ctx.lineTo(pBot.x, pBot.y);
        ctx.stroke();
        ctx.restore();
      }

      // 3. Cuerpo metálico principal (acero anodizado 3D con gradiente dinámico)
      const shimmerShift = Math.sin(timeSec * 1.9 + sideSign * 0.8) * 35 * unit;
      const bodyGrad = ctx.createLinearGradient(
        pMid.x - 40 * unit,
        pTop.y + shimmerShift,
        pTop.x + 40 * unit,
        pBot.y - shimmerShift
      );
      if (isDark) {
        bodyGrad.addColorStop(0, '#2B425E');
        bodyGrad.addColorStop(0.28, '#6B8FB8');
        bodyGrad.addColorStop(0.52, '#BFD7F2');
        bodyGrad.addColorStop(0.76, '#56789E');
        bodyGrad.addColorStop(1, '#1E3148');
      } else {
        bodyGrad.addColorStop(0, '#446385');
        bodyGrad.addColorStop(0.35, '#7FA1C7');
        bodyGrad.addColorStop(0.55, '#ADC8E6');
        bodyGrad.addColorStop(0.8, '#58789B');
        bodyGrad.addColorStop(1, '#2D4663');
      }

      ctx.save();
      ctx.strokeStyle = bodyGrad;
      ctx.lineWidth = thickness;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(pTop.x, pTop.y);
      ctx.lineTo(pMid.x, pMid.y);
      ctx.lineTo(pBot.x, pBot.y);
      ctx.stroke();
      ctx.restore();

      // 4. Bisel interior de sombra (da efecto de tubo cilíndrico torneado)
      ctx.save();
      ctx.strokeStyle = isDark ? 'rgba(15, 25, 42, 0.55)' : 'rgba(30, 48, 70, 0.35)';
      ctx.lineWidth = thickness * 0.58;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(pTop.x + sideSign * 1.8 * unit, pTop.y + 1.5 * unit);
      ctx.lineTo(pMid.x + sideSign * 1.8 * unit, pMid.y + 1.5 * unit);
      ctx.lineTo(pBot.x + sideSign * 1.8 * unit, pBot.y + 1.5 * unit);
      ctx.stroke();
      ctx.restore();

      // 5. Arista especular de cromo brillante (reflejo de luz de estudio superior)
      const specGrad = ctx.createLinearGradient(pMid.x, pTop.y, pTop.x, pBot.y);
      specGrad.addColorStop(0, 'rgba(255, 255, 255, 0.15)');
      specGrad.addColorStop(0.45, 'rgba(240, 248, 255, 0.88)');
      specGrad.addColorStop(0.55, 'rgba(255, 230, 175, 0.75)');
      specGrad.addColorStop(1, 'rgba(255, 255, 255, 0.12)');

      ctx.save();
      ctx.strokeStyle = specGrad;
      ctx.lineWidth = Math.max(0.9, thickness * 0.22);
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(pTop.x - sideSign * 2.2 * unit, pTop.y - 2.0 * unit);
      ctx.lineTo(pMid.x - sideSign * 2.2 * unit, pMid.y - 2.0 * unit);
      ctx.lineTo(pBot.x - sideSign * 2.2 * unit, pBot.y - 2.0 * unit);
      ctx.stroke();
      ctx.restore();

      if (!hyperMode) return;

      // 6. Pulso fotónico viajando por los brazos del corchete hacia el vértice central
      const pulseT = (timeSec * 0.65 + (sideSign < 0 ? 0 : 0.5)) % 1;
      // Dos fotones: uno desde el extremo superior al vértice y otro del vértice al inferior
      const photonPos = pulseT < 0.5
        ? {
            x: pTop.x + (pMid.x - pTop.x) * (pulseT * 2),
            y: pTop.y + (pMid.y - pTop.y) * (pulseT * 2)
          }
        : {
            x: pMid.x + (pBot.x - pMid.x) * ((pulseT - 0.5) * 2),
            y: pMid.y + (pBot.y - pMid.y) * ((pulseT - 0.5) * 2)
          };

      const pRadius = Math.max(2.0, thickness * 0.48);
      const pGlow = ctx.createRadialGradient(photonPos.x, photonPos.y, 0, photonPos.x, photonPos.y, pRadius * 2.8);
      pGlow.addColorStop(0, 'rgba(255, 248, 225, 0.95)');
      pGlow.addColorStop(0.35, 'rgba(246, 192, 86, 0.65)');
      pGlow.addColorStop(1, 'rgba(139, 168, 199, 0)');

      ctx.save();
      ctx.fillStyle = pGlow;
      ctx.beginPath();
      ctx.arc(photonPos.x, photonPos.y, pRadius * 2.8, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();

      // 7. Nodos terminales esféricos 3D en las puntas y vértice del corchete
      for (let idx = 0; idx < pts.length; idx++) {
        const pt = pts[idx];
        const capR = idx === 1 ? thickness * 0.34 : thickness * 0.28;
        const capGrad = ctx.createRadialGradient(
          pt.x - capR * 0.3,
          pt.y - capR * 0.3,
          capR * 0.05,
          pt.x,
          pt.y,
          capR
        );
        capGrad.addColorStop(0, '#FFFFFF');
        capGrad.addColorStop(0.4, idx === 1 ? '#F6C056' : '#B8D4F2');
        capGrad.addColorStop(1, 'rgba(30, 49, 72, 0)');

        ctx.save();
        ctx.fillStyle = capGrad;
        ctx.beginPath();
        ctx.arc(pt.x, pt.y, capR, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }
    }

    function renderRotatingHelix3D(ctx, width, height, dpr, phaseAngle, options, pointerTilt = { x: 0, y: 0 }, timeSec = 0) {
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, width, height);

      const isDark = options.theme === 'dark';
      const isScrolltide = (options.helixStyle || 'scrolltide') === 'scrolltide';
      const cx = width / 2;
      const cy = height / 2;
      const unit = width / 1000;

      // 1. Iluminación volumétrica multicapa detrás de la hélice
      const glow = ctx.createRadialGradient(cx, cy, 0, cx, cy, 310 * unit);
      if (isDark) {
        glow.addColorStop(0, 'rgba(139, 168, 199, 0.22)');
        glow.addColorStop(0.38, 'rgba(245, 186, 75, 0.11)');
        glow.addColorStop(0.72, 'rgba(36, 56, 80, 0.08)');
        glow.addColorStop(1, 'rgba(10, 15, 28, 0)');
      } else {
        glow.addColorStop(0, 'rgba(248, 213, 148, 0.48)');
        glow.addColorStop(0.52, 'rgba(139, 168, 199, 0.18)');
        glow.addColorStop(1, 'rgba(255, 255, 255, 0)');
      }
      ctx.fillStyle = glow;
      ctx.fillRect(0, 0, width, height);

      // 2. Retícula Blueprint & Anillos Orbitales Técnicos ("equal parts biology and blueprint")
      if (options.blueprintTicks) {
        ctx.save();
        ctx.strokeStyle = isDark ? 'rgba(139, 168, 199, 0.18)' : 'rgba(58, 90, 126, 0.16)';
        ctx.lineWidth = Math.max(0.8, 1.3 * unit);

        ctx.setLineDash([4 * unit, 6 * unit]);
        ctx.beginPath();
        ctx.moveTo(cx, 36 * unit);
        ctx.lineTo(cx, height - 36 * unit);
        ctx.stroke();
        ctx.setLineDash([]);

        // Doble anillo láser de escaneo con perspectiva 3D
        const scanWave = (Math.sin(timeSec * 1.25) + 1) * 0.5;
        const scanY = 82 * unit + scanWave * (height - 164 * unit);
        ctx.strokeStyle = isDark ? 'rgba(246, 192, 86, 0.28)' : 'rgba(217, 119, 6, 0.24)';
        ctx.beginPath();
        ctx.ellipse(cx, scanY, 98 * unit, 19 * unit, 0, 0, Math.PI * 2);
        ctx.stroke();

        const mLen = 13 * unit;
        const bx1 = 350 * unit, bx2 = 650 * unit;
        const by1 = 48 * unit, by2 = height - 48 * unit;
        ctx.strokeStyle = isDark ? 'rgba(139, 168, 199, 0.26)' : 'rgba(58, 90, 126, 0.22)';
        ctx.beginPath();
        ctx.moveTo(bx1, by1 + mLen); ctx.lineTo(bx1, by1); ctx.lineTo(bx1 + mLen, by1);
        ctx.moveTo(bx2 - mLen, by1); ctx.lineTo(bx2, by1); ctx.lineTo(bx2, by1 + mLen);
        ctx.moveTo(bx1, by2 - mLen); ctx.lineTo(bx1, by2); ctx.lineTo(bx1 + mLen, by2);
        ctx.moveTo(bx2 - mLen, by2); ctx.lineTo(bx2, by2); ctx.lineTo(bx2, by2 - mLen);
        ctx.stroke();
        ctx.restore();
      }

      // 3. Cinemática 3D de cámara compartida (Hélice + Corchetes < >)
      const tiltPitch = Math.sin(timeSec * 0.75) * 0.15 + (pointerTilt.y || 0) * 0.25;
      const tiltYaw = Math.cos(timeSec * 0.55) * 0.08 + (pointerTilt.x || 0) * 0.35;
      const fov = 360 * unit;

      // 4. Corchetes "<" y ">" con Movimiento 3D Sincronizado e Iluminación Metálica Hiperrealista
      const projectBracketVertex = (rawX, rawY, sideSign) => {
        // Respiración magnética acoplada al giro de la hélice + flotación vertical desfasada
        const pulseExpand = Math.sin(timeSec * 1.85) * 9.5 * unit * sideSign;
        const floatY = Math.cos(timeSec * 1.35 + sideSign * 0.9) * 6.5 * unit;
        // Rotación 3D tipo "aleta" (los corchetes basculan en profundidad Z)
        const wingAngle = Math.sin(timeSec * 1.15 + sideSign * 0.6) * 0.26 + tiltYaw * 0.7;

        const localX = (rawX - cx) + pulseExpand;
        const localY = (rawY - cy) + floatY;
        // Cuanto más cerca del vértice exterior, mayor desplazamiento Z
        const distFromPivot = Math.abs(localX) - 220 * unit;
        const z3d = Math.sin(wingAngle) * distFromPivot * sideSign + localY * Math.sin(tiltPitch * 0.65);
        const y3d = localY * Math.cos(tiltPitch * 0.65);

        const persp = fov / (fov - z3d * 0.45);
        return {
          x: cx + localX * persp,
          y: cy + y3d * persp,
          persp
        };
      };

      const bThickness = Math.max(2.4, 26 * unit);
      const leftPts = [
        projectBracketVertex(282 * unit, 184 * unit, -1),
        projectBracketVertex(172 * unit, cy, -1),
        projectBracketVertex(282 * unit, height - 184 * unit, -1)
      ];
      const rightPts = [
        projectBracketVertex(718 * unit, 184 * unit, 1),
        projectBracketVertex(828 * unit, cy, 1),
        projectBracketVertex(718 * unit, height - 184 * unit, 1)
      ];

      // Ondas de campo energético emitidas desde el vértice de "<" y ">" hacia el ADN
      if (isScrolltide) {
        const wavePhase = (timeSec * 0.75) % 1;
        for (const [vPt, dir] of [[leftPts[1], 1], [rightPts[1], -1]]) {
          const arcX = vPt.x + dir * (18 + wavePhase * 55) * unit;
          const arcAlpha = (1 - wavePhase) * (isDark ? 0.34 : 0.24);
          ctx.save();
          ctx.strokeStyle = isDark ? `rgba(166, 193, 223, ${arcAlpha})` : `rgba(58, 90, 126, ${arcAlpha})`;
          ctx.lineWidth = Math.max(0.8, 2.2 * (1 - wavePhase * 0.6) * unit);
          ctx.beginPath();
          ctx.arc(arcX, vPt.y, (28 + wavePhase * 42) * unit, dir > 0 ? -0.65 : Math.PI - 0.65, dir > 0 ? 0.65 : Math.PI + 0.65);
          ctx.stroke();
          ctx.restore();
        }
      }

      drawMetallicBracket3D(ctx, leftPts, bThickness * leftPts[1].persp, isDark, timeSec, -1, unit, isScrolltide);
      drawMetallicBracket3D(ctx, rightPts, bThickness * rightPts[1].persp, isDark, timeSec, 1, unit, isScrolltide);

      // 5. Cinemática 3D Hiperrealista de la Doble Hélice
      const topY = 64 * unit;
      const botY = height - 64 * unit;
      const helixH = botY - topY;
      const baseRadiusX = 125 * unit;
      const turns = 1.6;

      const project3DHelix = (v, strandOffset, customRadiusMult = 1) => {
        const twistWave = Math.sin(v * Math.PI * 2 - timeSec * 1.8) * 0.16;
        const theta = v * Math.PI * 2 * turns - phaseAngle + strandOffset + twistWave + tiltYaw;

        const envelope = 0.88 + Math.sin(v * Math.PI) * 0.18;
        const r = baseRadiusX * envelope * customRadiusMult;

        const x3d = Math.cos(theta) * r;
        const yCentered = (v - 0.5) * helixH;
        const zRaw = Math.sin(theta) * r;

        const y3d = yCentered * Math.cos(tiltPitch) - zRaw * Math.sin(tiltPitch);
        const z3d = yCentered * Math.sin(tiltPitch) + zRaw * Math.cos(tiltPitch);

        const zNorm = Math.max(-1, Math.min(1, z3d / (baseRadiusX * 1.15)));
        const persp = fov / (fov - z3d * 0.44);

        return {
          x: cx + x3d * persp,
          y: cy + y3d * persp,
          z: zNorm,
          persp,
          v
        };
      };

      const renderQueue = [];

      // A) Partículas cuánticas con bokeh 3D
      for (const d of (options.blueprintTicks ? HELIX_DUST : [])) {
        const vAnim = (d.v + timeSec * 0.04 * d.speed) % 1;
        const pt = project3DHelix(vAnim, d.angle + phaseAngle * (d.speed * 0.6), d.dist);
        renderQueue.push({
          type: 'dust',
          z: pt.z,
          x: pt.x,
          y: pt.y,
          persp: pt.persp,
          size: d.size,
          gold: d.gold,
          twinkle: Math.sin(timeSec * 3.2 + d.angle * 4) * 0.5 + 0.5
        });
      }

      // B) Pares de bases (16 puentes moleculares con enlaces de hidrógeno y fotones)
      const numRungs = 10;
      for (let i = 0; i < numRungs; i++) {
        const v = (i + 0.5) / numRungs;
        const pA = project3DHelix(v, 0);
        const pB = project3DHelix(v, Math.PI);
        const avgZ = (pA.z + pB.z) * 0.5;

        const pulsePos = (Math.sin(timeSec * 2.6 - i * 0.55) + 1) * 0.5;
        const verticalWave = Math.pow(Math.max(0, Math.cos((v - ((timeSec * 0.28) % 1)) * Math.PI * 2)), 4);

        renderQueue.push({
          type: 'rung',
          z: avgZ - 0.03,
          pA,
          pB,
          index: i,
          pulsePos,
          verticalWave
        });

        renderQueue.push({
          type: 'node',
          strand: 'A',
          z: pA.z + 0.025,
          pt: pA,
          energy: verticalWave
        });
        renderQueue.push({
          type: 'node',
          strand: 'B',
          z: pB.z + 0.025,
          pt: pB,
          energy: verticalWave
        });
      }

      // C) Hebras continuas cilíndricas 3D (72 segmentos de alta resolución)
      const steps = 72;
      for (let s = 0; s < steps; s++) {
        const v0 = s / steps;
        const v1 = (s + 1) / steps;

        const a0 = project3DHelix(v0, 0);
        const a1 = project3DHelix(v1, 0);
        renderQueue.push({
          type: 'strand',
          strand: 'A',
          z: (a0.z + a1.z) * 0.5,
          p0: a0,
          p1: a1
        });

        const b0 = project3DHelix(v0, Math.PI);
        const b1 = project3DHelix(v1, Math.PI);
        renderQueue.push({
          type: 'strand',
          strand: 'B',
          z: (b0.z + b1.z) * 0.5,
          p0: b0,
          p1: b1
        });

        if (isScrolltide && s % 2 === 0) {
          const w0 = project3DHelix(v0, 0.24, 1.09);
          const w1 = project3DHelix(v1, 0.24, 1.09);
          renderQueue.push({
            type: 'wire',
            z: (w0.z + w1.z) * 0.5 - 0.05,
            p0: w0,
            p1: w1
          });
        }
      }

      // Ordenar estrictamente por coordenada Z (Pintor 3D real)
      renderQueue.sort((m, n) => m.z - n.z);

      // 6. Renderizado Hiperrealista con Iluminación Esférica PBR y Depth-of-Field
      for (const item of renderQueue) {
        if (item.type === 'dust') {
          const depthAlpha = (0.16 + (item.z + 1) * 0.36) * (0.45 + item.twinkle * 0.55);
          // Bokeh: las partículas muy cercanas o muy lejanas son más suaves y grandes
          const dofBlur = Math.abs(item.z) > 0.65 ? 1.45 : 1.0;
          const r = Math.max(0.7, item.size * unit * item.persp * dofBlur);
          ctx.save();
          const dGrad = ctx.createRadialGradient(item.x, item.y, 0, item.x, item.y, r * 1.8);
          if (item.gold) {
            dGrad.addColorStop(0, `rgba(255, 240, 195, ${depthAlpha})`);
            dGrad.addColorStop(0.5, `rgba(246, 192, 86, ${depthAlpha * 0.6})`);
            dGrad.addColorStop(1, 'rgba(246, 192, 86, 0)');
          } else {
            dGrad.addColorStop(0, isDark ? `rgba(220, 238, 255, ${depthAlpha})` : `rgba(58, 90, 126, ${depthAlpha})`);
            dGrad.addColorStop(0.5, isDark ? `rgba(139, 168, 199, ${depthAlpha * 0.55})` : `rgba(88, 120, 155, ${depthAlpha * 0.5})`);
            dGrad.addColorStop(1, 'rgba(139, 168, 199, 0)');
          }
          ctx.fillStyle = dGrad;
          ctx.beginPath();
          ctx.arc(item.x, item.y, r * 1.8, 0, Math.PI * 2);
          ctx.fill();
          ctx.restore();

        } else if (item.type === 'wire') {
          const dn = (item.z + 1) * 0.5;
          ctx.save();
          ctx.strokeStyle = isDark
            ? `rgba(155, 190, 230, ${0.06 + dn * 0.24})`
            : `rgba(58, 90, 126, ${0.07 + dn * 0.18})`;
          ctx.lineWidth = Math.max(0.5, 1.6 * unit);
          ctx.beginPath();
          ctx.moveTo(item.p0.x, item.p0.y);
          ctx.lineTo(item.p1.x, item.p1.y);
          ctx.stroke();
          ctx.restore();

        } else if (item.type === 'rung') {
          const { pA, pB, pulsePos, verticalWave } = item;
          const dx = pB.x - pA.x;
          const dy = pB.y - pA.y;
          const dist = Math.hypot(dx, dy);
          if (dist < 6 * unit) continue;

          const avgPersp = (pA.persp + pB.persp) * 0.5;
          const depthNorm = (item.z + 1) * 0.5;
          const alpha = 0.26 + depthNorm * 0.72;
          const midX = (pA.x + pB.x) * 0.5;
          const midY = (pA.y + pB.y) * 0.5;

          ctx.save();
          if (isScrolltide) {
            // Cilindro traslúcido de par de bases con núcleo luminoso
            const rungGrad = ctx.createLinearGradient(pA.x, pA.y, pB.x, pB.y);
            rungGrad.addColorStop(0, `rgba(246, 192, 86, ${alpha})`);
            rungGrad.addColorStop(0.36, `rgba(232, 168, 56, ${alpha * 0.9})`);
            rungGrad.addColorStop(0.5, `rgba(255, 245, 220, ${alpha * 0.95})`);
            rungGrad.addColorStop(0.64, `rgba(122, 158, 196, ${alpha * 0.9})`);
            rungGrad.addColorStop(1, `rgba(175, 206, 238, ${alpha})`);

            const rWidth = Math.max(1.2, (5.2 + verticalWave * 2.4) * unit * avgPersp);
            ctx.strokeStyle = rungGrad;
            ctx.lineWidth = rWidth;
            ctx.lineCap = 'round';
            ctx.beginPath();
            ctx.moveTo(pA.x, pA.y);
            ctx.lineTo(pB.x, pB.y);
            ctx.stroke();

            // Brillo especular longitudinal sobre el cilindro del peldaño
            ctx.strokeStyle = `rgba(255, 255, 255, ${alpha * 0.42})`;
            ctx.lineWidth = Math.max(0.5, rWidth * 0.28);
            ctx.beginPath();
            ctx.moveTo(pA.x, pA.y - 0.9 * unit);
            ctx.lineTo(pB.x, pB.y - 0.9 * unit);
            ctx.stroke();

            // Unión de puente de hidrógeno central (cápsula metálica gris/oro en el centro del peldaño)
            if (dist > 22 * unit) {
              const bondR = Math.max(1.2, 2.8 * unit * avgPersp);
              const bondGrad = ctx.createRadialGradient(midX - bondR * 0.3, midY - bondR * 0.3, 0, midX, midY, bondR * 1.4);
              bondGrad.addColorStop(0, `rgba(255, 255, 255, ${alpha})`);
              bondGrad.addColorStop(0.5, `rgba(180, 170, 160, ${alpha * 0.9})`);
              bondGrad.addColorStop(1, 'rgba(90, 82, 74, 0)');
              ctx.fillStyle = bondGrad;
              ctx.beginPath();
              ctx.arc(midX, midY, bondR * 1.4, 0, Math.PI * 2);
              ctx.fill();
            }

            // Fotón bio-eléctrico hiperrealista recorriendo el puente
            const px = pA.x + dx * pulsePos;
            const py = pA.y + dy * pulsePos;
            const pulseR = Math.max(1.5, (3.8 + verticalWave * 2.2) * unit * avgPersp);
            const pGlow = ctx.createRadialGradient(px, py, 0, px, py, pulseR * 2.4);
            pGlow.addColorStop(0, `rgba(255, 252, 235, ${Math.min(1, alpha + 0.3)})`);
            pGlow.addColorStop(0.4, `rgba(246, 192, 86, ${alpha * 0.85})`);
            pGlow.addColorStop(1, 'rgba(246, 192, 86, 0)');
            ctx.fillStyle = pGlow;
            ctx.beginPath();
            ctx.arc(px, py, pulseR * 2.4, 0, Math.PI * 2);
            ctx.fill();
          } else {
            const rungThickness = Math.max(1.5, 7.2 * unit * avgPersp);
            const bStart = 0.28, bEnd = 0.72;
            const bx1 = pA.x + dx * bStart, by1 = pA.y + dy * bStart;
            const bx2 = pA.x + dx * bEnd, by2 = pA.y + dy * bEnd;

            ctx.lineCap = 'round';
            ctx.lineWidth = rungThickness;
            ctx.strokeStyle = isDark ? `rgba(246, 192, 86, ${alpha})` : `rgba(242, 178, 61, ${alpha})`;
            ctx.beginPath();
            ctx.moveTo(pA.x, pA.y); ctx.lineTo(bx1, by1);
            ctx.moveTo(bx2, by2); ctx.lineTo(pB.x, pB.y);
            ctx.stroke();

            ctx.lineCap = 'butt';
            ctx.strokeStyle = isDark ? `rgba(152, 140, 128, ${alpha})` : `rgba(140, 128, 116, ${alpha})`;
            ctx.beginPath();
            ctx.moveTo(bx1, by1); ctx.lineTo(bx2, by2);
            ctx.stroke();
          }
          ctx.restore();

        } else if (item.type === 'strand') {
          const { p0, p1, z, strand } = item;
          const depthNorm = (z + 1) * 0.5;
          const baseW = isScrolltide
            ? (strand === 'A' ? 11.5 : 10.2) * unit
            : (strand === 'A' ? 16.5 : 15.5) * unit;
          const thickness = Math.max(1.6, baseW * (0.62 + depthNorm * 0.58));
          const alpha = 0.24 + depthNorm * 0.76;

          ctx.save();
          ctx.lineCap = 'round';

          // Capa 1: Base oscura de volumen (sombra de borde cilíndrico 3D)
          ctx.lineWidth = thickness;
          ctx.strokeStyle = strand === 'A'
            ? (isDark ? `rgba(48, 76, 108, ${alpha})` : `rgba(45, 70, 98, ${alpha})`)
            : (isDark ? `rgba(24, 40, 62, ${alpha})` : `rgba(22, 36, 54, ${alpha})`);
          ctx.beginPath();
          ctx.moveTo(p0.x, p0.y);
          ctx.lineTo(p1.x, p1.y);
          ctx.stroke();

          // Capa 2: Cuerpo difuso principal desplazado hacia la fuente de luz
          ctx.lineWidth = thickness * 0.72;
          ctx.strokeStyle = strand === 'A'
            ? (isDark ? `rgba(138, 174, 212, ${alpha})` : `rgba(98, 132, 168, ${alpha})`)
            : (isDark ? `rgba(68, 102, 140, ${alpha})` : `rgba(42, 66, 94, ${alpha})`);
          ctx.beginPath();
          ctx.moveTo(p0.x - 0.8 * unit, p0.y - 0.8 * unit);
          ctx.lineTo(p1.x - 0.8 * unit, p1.y - 0.8 * unit);
          ctx.stroke();

          // Capa 3: Filamento especular de cristal en el lomo frontal de la hebra
          if (depthNorm > 0.38) {
            ctx.lineWidth = Math.max(0.6, thickness * 0.24);
            ctx.strokeStyle = strand === 'A'
              ? `rgba(240, 248, 255, ${(depthNorm - 0.3) * 1.15})`
              : `rgba(248, 210, 130, ${(depthNorm - 0.3) * 0.85})`;
            ctx.beginPath();
            ctx.moveTo(p0.x - 1.6 * unit, p0.y - 1.4 * unit);
            ctx.lineTo(p1.x - 1.6 * unit, p1.y - 1.4 * unit);
            ctx.stroke();
          }
          ctx.restore();

        } else if (item.type === 'node') {
          const { pt, z, strand, energy } = item;
          const depthNorm = (z + 1) * 0.5;
          const nodeR = Math.max(1.8, (6.0 + depthNorm * 4.4 + energy * 2.5) * unit * pt.persp);
          const alpha = 0.32 + depthNorm * 0.68;

          ctx.save();
          // 1. Halo de dispersión atmosférica alrededor de la esfera atómica
          if (depthNorm > 0.35) {
            const haloGrad = ctx.createRadialGradient(pt.x, pt.y, nodeR * 0.4, pt.x, pt.y, nodeR * 2.2);
            if (strand === 'A') {
              haloGrad.addColorStop(0, `rgba(166, 193, 223, ${alpha * 0.42})`);
              haloGrad.addColorStop(1, 'rgba(166, 193, 223, 0)');
            } else {
              haloGrad.addColorStop(0, `rgba(246, 192, 86, ${alpha * 0.45})`);
              haloGrad.addColorStop(1, 'rgba(246, 192, 86, 0)');
            }
            ctx.fillStyle = haloGrad;
            ctx.beginPath();
            ctx.arc(pt.x, pt.y, nodeR * 2.2, 0, Math.PI * 2);
            ctx.fill();
          }

          // 2. Esfera 3D con sombreado PBR (luz cenital izquierda, subsurface scattering y sombra inferior)
          const lightX = pt.x - nodeR * 0.34;
          const lightY = pt.y - nodeR * 0.34;
          const sphereGrad = ctx.createRadialGradient(lightX, lightY, nodeR * 0.06, pt.x, pt.y, nodeR);

          if (strand === 'A') {
            sphereGrad.addColorStop(0, `rgba(255, 255, 255, ${alpha})`);
            sphereGrad.addColorStop(0.28, `rgba(195, 222, 250, ${alpha})`);
            sphereGrad.addColorStop(0.68, `rgba(92, 130, 172, ${alpha})`);
            sphereGrad.addColorStop(1, `rgba(22, 38, 58, ${alpha})`);
          } else {
            sphereGrad.addColorStop(0, `rgba(255, 252, 235, ${alpha})`);
            sphereGrad.addColorStop(0.28, `rgba(252, 206, 108, ${alpha})`);
            sphereGrad.addColorStop(0.7, `rgba(196, 122, 24, ${alpha})`);
            sphereGrad.addColorStop(1, `rgba(68, 36, 8, ${alpha})`);
          }

          ctx.fillStyle = sphereGrad;
          ctx.beginPath();
          ctx.arc(pt.x, pt.y, nodeR, 0, Math.PI * 2);
          ctx.fill();

          // 3. Punto especular cristalino (reflejo directo de fuente de luz)
          if (depthNorm > 0.42) {
            ctx.fillStyle = `rgba(255, 255, 255, ${depthNorm * 0.92})`;
            ctx.beginPath();
            ctx.arc(lightX, lightY, Math.max(0.6, nodeR * 0.24), 0, Math.PI * 2);
            ctx.fill();
          }
          ctx.restore();
        }
      }
    }

    /**
     * =========================================================================
     * 3. WEB COMPONENT UNIFICADO <rsj-brand>
     *    - Arriba: Doble hélice ADN 3D rotando entre < >
     *    - Abajo: Firma de partículas 3D "RSJ" limpia (sin corchetes extra)
     * =========================================================================
     */
    class RSJBrand extends HTMLElement {
      connectedCallback() {
        if (this._ready) return;
        this._ready = true;

        const root = this.shadowRoot || this.attachShadow({ mode: 'open' });
        root.innerHTML = `
          <style>
            :host {
              display: inline-flex;
              flex-direction: column;
              align-items: center;
              width: ${STATE.scaleWidth}px;
              max-width: 92vw;
              contain: content;
              user-select: none;
              cursor: pointer;
            }
            .dna-box {
              width: 100%;
              aspect-ratio: 1000 / 580;
              display: block;
            }
            .sig-box {
              width: 90%;
              aspect-ratio: 2.75;
              margin-top: ${STATE.gapPx}px;
              display: block;
            }
            canvas {
              width: 100%;
              height: 100%;
              display: block;
            }
          </style>
          <div class="dna-box">
            <canvas class="dna-canvas" role="img" aria-label="Isotipo RSJ: Doble hélice de ADN 3D girando entre corchetes de código"></canvas>
          </div>
          <div class="sig-box">
            <canvas class="sig-canvas" role="img" aria-label="RSJ, Raúl Salazar Jamen: firma de código animada con partículas"></canvas>
          </div>
        `;

        this._dnaBox = root.querySelector('.dna-box');
        this._sigBox = root.querySelector('.sig-box');
        this._dnaCanvas = root.querySelector('.dna-canvas');
        this._sigCanvas = root.querySelector('.sig-canvas');
        this._dnaCtx = this._dnaCanvas.getContext('2d');
        this._sigCtx = this._sigCanvas.getContext('2d');

        this._start = performance.now();
        this._lastFrameTime = performance.now();
        this._helixAngle = 0.35;
        this._hoverBoost = 0;
        this._pointerTarget = { x: 0, y: 0 };
        this._pointerCurrent = { x: 0, y: 0 };

        this.addEventListener('pointerenter', () => {
          if (STATE.hoverInteraction) this._hoverBoost = 1;
        });
        this.addEventListener('pointermove', (e) => {
          if (!STATE.hoverInteraction) return;
          const rect = this.getBoundingClientRect();
          const nx = ((e.clientX - rect.left) / Math.max(1, rect.width)) * 2 - 1;
          const ny = ((e.clientY - rect.top) / Math.max(1, rect.height)) * 2 - 1;
          this._pointerTarget.x = Math.max(-1, Math.min(1, nx));
          this._pointerTarget.y = Math.max(-1, Math.min(1, ny));
        });
        this.addEventListener('pointerleave', () => {
          this._hoverBoost = 0;
          this._pointerTarget.x = 0;
          this._pointerTarget.y = 0;
        });

        this.rebuildMask();

        const reduced = matchMedia('(prefers-reduced-motion: reduce)');
        let dnaW = 240, dnaH = 139, sigW = 216, sigH = 78, dpr = 1, visible = true;

        const resize = () => {
          const dRect = this._dnaBox.getBoundingClientRect();
          const sRect = this._sigBox.getBoundingClientRect();
          dnaW = dRect.width || 240;
          dnaH = dRect.height || 139;
          sigW = sRect.width || 216;
          sigH = sRect.height || 78;
          dpr = Math.min(devicePixelRatio || 1, 2);

          this._dnaCanvas.width = Math.round(dnaW * dpr);
          this._dnaCanvas.height = Math.round(dnaH * dpr);
          this._sigCanvas.width = Math.round(sigW * dpr);
          this._sigCanvas.height = Math.round(sigH * dpr);

          this.drawFrame(performance.now(), dnaW, dnaH, sigW, sigH, dpr, reduced.matches);
        };

        this._resize = new ResizeObserver(resize);
        this._resize.observe(this);

        this._intersection = new IntersectionObserver((entries) => {
          visible = entries[0].isIntersecting;
        });
        this._intersection.observe(this);

        this._motion = () => this.drawFrame(performance.now(), dnaW, dnaH, sigW, sigH, dpr, reduced.matches);
        reduced.addEventListener('change', this._motion);
        this._reduced = reduced;

        const tick = (now) => {
          if (visible && !reduced.matches) {
            this.drawFrame(now, dnaW, dnaH, sigW, sigH, dpr, false);
          } else {
            this._lastFrameTime = now;
          }
          this._raf = requestAnimationFrame(tick);
        };

        resize();
        this._raf = requestAnimationFrame(tick);
      }

      updateLayout() {
        this.style.width = `${STATE.scaleWidth}px`;
        if (this._sigBox) {
          this._sigBox.style.marginTop = `${STATE.gapPx}px`;
        }
      }

      rebuildMask() {
        const mask = document.createElement('canvas');
        mask.width = 1000;
        mask.height = 360;
        const mc = mask.getContext('2d', { willReadFrequently: true });

        // Solo las letras RSJ (sin corchetes laterales)
        mc.fillStyle = 'white';
        mc.font = STATE.fontStyle === 'tech'
          ? '700 215px "Space Grotesk", Arial, sans-serif'
          : '900 220px Arial, sans-serif';
        mc.textAlign = 'center';
        mc.textBaseline = 'middle';
        mc.fillText('RSJ', 500, 185);

        const pixels = mc.getImageData(0, 0, 1000, 360).data;

        let seed = 1729;
        const random = () => {
          seed = (seed * 1664525 + 1013904223) >>> 0;
          return seed / 4294967296;
        };

        const points = [];
        for (let y = 55; y < 305; y += 5) {
          for (let x = 220; x < 780; x += 5) {
            if (pixels[(y * 1000 + x) * 4 + 3] > 140) {
              const a = random() * Math.PI * 2;
              const r = 55 + Math.pow(random(), 0.5) * 300;
              points.push({
                x: x - 500,
                y: y - 180,
                a,
                r,
                z: random() * 2 - 1,
                size: 0.65 + random() * 1.3,
                tw: random() * Math.PI * 2,
                star: random() < 0.035
              });
            }
          }
        }
        this._points = points;

        const statsEl = document.getElementById('particle-stats');
        if (statsEl) statsEl.textContent = `${points.length} pts`;
      }

      restartCycle() {
        this._start = performance.now();
      }

      drawFrame(now, dnaW, dnaH, sigW, sigH, dpr, isReduced) {
        const dt = Math.min(0.05, (now - this._lastFrameTime) / 1000);
        this._lastFrameTime = now;
        const timeSec = (now - this._start) / 1000;

        // Suavizado de inclinación 3D interactiva con el cursor
        this._pointerCurrent.x += (this._pointerTarget.x - this._pointerCurrent.x) * 0.08;
        this._pointerCurrent.y += (this._pointerTarget.y - this._pointerCurrent.y) * 0.08;

        // 1. Actualizar rotación continua de la hélice 3D
        if (!isReduced && STATE.helixSpeed > 0) {
          const speedMult = 1 + this._hoverBoost * 0.95;
          this._helixAngle += dt * STATE.helixSpeed * speedMult;
        }

        // Renderizar Hélice ADN 3D estilo Scrolltide Helix arriba
        if (this._dnaCtx && dnaW > 0) {
          renderRotatingHelix3D(
            this._dnaCtx,
            dnaW,
            dnaH,
            dpr,
            this._helixAngle,
            STATE,
            this._pointerCurrent,
            isReduced ? 2.5 : timeSec
          );
        }

        // 2. Renderizar Partículas RSJ abajo
        const ctx = this._sigCtx;
        const width = sigW;
        const height = sigH;
        if (!ctx || !width || !this._points) return;

        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);

        const ease = (t) => {
          t = Math.max(0, Math.min(1, t));
          return t * t * (3 - 2 * t);
        };

        const scale = width / 1000;
        const elapsed = ((now - this._start) / 1000) % 14;
        const t = isReduced ? 6 : elapsed;

        let assembly = 1;
        if (STATE.assemblyMode === 'locked') {
          assembly = 1;
        } else if (STATE.assemblyMode === 'longhold') {
          assembly = t < 2.2 ? ease(t / 2.2) : t < 11.8 ? 1 : t < 13.5 ? 1 - ease((t - 11.8) / 1.7) : 0;
        } else {
          assembly = t < 4 ? ease(t / 4) : t < 8.5 ? 1 : t < 12 ? 1 - ease((t - 8.5) / 3.5) : 0;
        }

        // Actualizar barra de telemetría
        const barEl = document.getElementById('cycle-bar');
        const timeEl = document.getElementById('cycle-time-text');
        const badgeEl = document.getElementById('phase-badge');
        if (barEl && timeEl && badgeEl) {
          if (STATE.assemblyMode === 'locked') {
            barEl.style.width = '100%';
            timeEl.textContent = 'FIJO';
            badgeEl.textContent = 'Helix 3D Activo · RSJ Fijo';
          } else {
            barEl.style.width = `${(t / 14) * 100}%`;
            timeEl.textContent = `${t.toFixed(1)}s`;
            badgeEl.textContent = assembly > 0.96
              ? 'RSJ Ensamblado'
              : assembly < 0.08
                ? 'Órbita Libre'
                : 'Convergiendo...';
          }
        }

        const orbit = now * 0.00017;
        const isDark = STATE.theme === 'dark';

        const glow = ctx.createRadialGradient(width / 2, height / 2, 0, width / 2, height / 2, width * 0.42);
        if (isDark) {
          glow.addColorStop(0, `rgba(158, 173, 216, ${0.035 + 0.035 * (1 - assembly)})`);
          glow.addColorStop(1, 'rgba(140, 155, 205, 0)');
        } else {
          glow.addColorStop(0, `rgba(98, 129, 166, ${0.04 + 0.04 * (1 - assembly)})`);
          glow.addColorStop(1, 'rgba(98, 129, 166, 0)');
        }
        ctx.fillStyle = glow;
        ctx.fillRect(0, 0, width, height);

        for (const p of this._points) {
          const a = p.a + orbit + (p.y / 250) * 3.4;
          const z = Math.sin(a) * 0.65 + p.z * 0.35;
          const perspective = 1 / (1 - z * 0.26 * (1 - assembly));
          const sx = Math.cos(a) * p.r;
          const sy = Math.sin(a) * p.r * 0.45 + p.y * 0.25;
          const drift = Math.sin(now * 0.0006 + p.tw) * 1.1;

          const x = width / 2 + (sx * (1 - assembly) + (p.x + drift) * assembly) * scale * perspective;
          const y = height / 2 + (sy * (1 - assembly) + (p.y + drift * 0.5) * assembly) * scale * perspective;

          const alpha = (0.36 + 0.53 * (z + 1) / 2) * (0.77 + 0.23 * Math.sin(now * 0.001 + p.tw));
          const radius = Math.max(0.35, p.size * scale * (1 + 0.7 * (1 - assembly)));

          if (isDark) {
            ctx.fillStyle = p.star
              ? `rgba(246, 192, 86, ${Math.min(1, alpha * 1.15)})`
              : `rgba(220, 229, 255, ${alpha})`;
            ctx.shadowColor = p.star ? 'rgba(246, 192, 86, 0.75)' : 'rgba(180, 197, 255, 0.65)';
          } else {
            ctx.fillStyle = p.star
              ? `rgba(217, 119, 6, ${Math.min(1, alpha * 1.2)})`
              : `rgba(36, 56, 80, ${Math.min(1, alpha * 1.15)})`;
            ctx.shadowColor = p.star ? 'rgba(245, 158, 11, 0.55)' : 'rgba(58, 90, 126, 0.35)';
          }

          ctx.shadowBlur = p.star ? 6 * scale : 0;
          if (z < -0.35 && assembly < 0.95) {
            ctx.shadowBlur = 9 * scale;
            ctx.globalAlpha = 0.55;
          } else {
            ctx.globalAlpha = 1;
          }

          ctx.beginPath();
          ctx.arc(x, y, radius, 0, Math.PI * 2);
          ctx.fill();

          if (p.star && width > 130) {
            const s = radius * (2.4 + Math.sin(now * 0.001 + p.tw));
            ctx.strokeStyle = isDark
              ? `rgba(248, 213, 148, ${alpha * 0.75})`
              : `rgba(217, 119, 6, ${alpha * 0.75})`;
            ctx.lineWidth = Math.max(0.4, scale * 0.75);
            ctx.beginPath();
            ctx.moveTo(x - s, y);
            ctx.lineTo(x + s, y);
            ctx.moveTo(x, y - s);
            ctx.lineTo(x, y + s);
            ctx.stroke();
          }
        }

        ctx.globalAlpha = 1;
        ctx.shadowBlur = 0;
      }

      disconnectedCallback() {
        cancelAnimationFrame(this._raf);
        this._resize?.disconnect();
        this._intersection?.disconnect();
        this._reduced?.removeEventListener('change', this._motion);
        this._ready = false;
        this.shadowRoot?.replaceChildren();
      }
    }

    customElements.define('rsj-brand', RSJBrand);
function mountSignature() {
 const support=document.querySelector('.sidebar-support');
 if(!support||support.querySelector('.rsj-signature'))return;
 const signature=document.createElement('div');
 signature.className='rsj-signature';
 signature.setAttribute('role','img');
 signature.setAttribute('aria-label','RSJ — Raúl Salazar Jamen');
 signature.title='RSJ — Raúl Salazar Jamen';
 signature.innerHTML='<rsj-brand aria-hidden="true"></rsj-brand>';
 support.appendChild(signature);
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mountSignature);else mountSignature();
})();