const WEATHER_CODES = {
  0: 'Clear sky', 1: 'Mainly clear', 2: 'Partly cloudy', 3: 'Overcast',
  45: 'Fog', 48: 'Fog', 51: 'Drizzle', 61: 'Rain', 63: 'Rain', 65: 'Heavy rain',
  71: 'Snow', 73: 'Snow', 75: 'Heavy snow', 80: 'Rain showers', 95: 'Thunderstorm',
};

window.islaModules.renderers.weather = async function (el) {
  const city = await window.isla.storeGet('weatherCity', null);
  if (!city) {
    el.innerHTML = cityPrompt();
    el.querySelector('#weatherSave').addEventListener('click', async () => {
      const name = el.querySelector('#weatherCityInput').value.trim();
      if (!name) return;
      await window.isla.storeSet('weatherCity', name);
      window.islaModules.render('weather', el);
    });
    return;
  }

  el.innerHTML = '<div class="weather-loading">Loading weather...</div>';
  try {
    const data = await fetchWeather(city);
    el.innerHTML = weatherView(city, data);
    el.querySelector('#weatherChange').addEventListener('click', async () => {
      await window.isla.storeSet('weatherCity', null);
      window.islaModules.render('weather', el);
    });
  } catch (e) {
    el.innerHTML = `<div class="weather-loading">Couldn't load weather for "${city}". <button id="weatherChange">Change city</button></div>`;
    el.querySelector('#weatherChange')?.addEventListener('click', async () => {
      await window.isla.storeSet('weatherCity', null);
      window.islaModules.render('weather', el);
    });
  }
};

function cityPrompt() {
  return `
    <div class="weather-setup">
      <div class="weather-loading">Which city?</div>
      <input id="weatherCityInput" class="weather-input" placeholder="e.g. Pune" />
      <button id="weatherSave">Save</button>
    </div>`;
}

async function fetchWeather(city) {
  const geoRes = await fetch(`https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(city)}&count=1`);
  const geo = await geoRes.json();
  const place = geo.results?.[0];
  if (!place) throw new Error('city not found');

  const wRes = await fetch(`https://api.open-meteo.com/v1/forecast?latitude=${place.latitude}&longitude=${place.longitude}&current=temperature_2m,weather_code,relative_humidity_2m,wind_speed_10m&timezone=auto`);
  const w = await wRes.json();
  return { place, current: w.current };
}

function weatherView(city, { place, current }) {
  const desc = WEATHER_CODES[current.weather_code] || 'Clear';
  return `
    <div class="weather">
      <div class="weather-top">
        <div class="weather-temp">${Math.round(current.temperature_2m)}&deg;</div>
        <div class="weather-desc">${desc}<br/><span class="card-label">${place.name}</span></div>
      </div>
      <div class="weather-stats">
        <div><span class="card-label">HUMIDITY</span><div>${current.relative_humidity_2m}%</div></div>
        <div><span class="card-label">WIND</span><div>${Math.round(current.wind_speed_10m)} km/h</div></div>
      </div>
      <button id="weatherChange" class="weather-change">Change city</button>
    </div>`;
}
