FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

ARG INSTALL_HF_DEPS=false

RUN if [ "$INSTALL_HF_DEPS" = "true" ]; then \
      apt-get update \
      && apt-get install -y --no-install-recommends ffmpeg \
      && rm -rf /var/lib/apt/lists/*; \
    fi

COPY requirements.txt requirements.hf.txt ./
RUN pip install --no-cache-dir -r requirements.txt
RUN if [ "$INSTALL_HF_DEPS" = "true" ]; then \
      pip install --no-cache-dir -r requirements.hf.txt; \
    fi

COPY app ./app
COPY tests ./tests

EXPOSE 8010

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8010"]
